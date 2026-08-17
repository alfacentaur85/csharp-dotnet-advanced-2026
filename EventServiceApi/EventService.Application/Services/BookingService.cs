using EventService.Domain.Enums;
using EventService.Domain.Exceptions;
using EventService.Application.Interfaces;
using EventService.Application.Options;
using EventService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace EventService.Application.Services;

/// <summary>
/// Реализация сервиса броней (бизнес-логика и конкурентность; доступ к данным — через репозитории).
/// </summary>
public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _maxActiveBookingsPerUser;

    private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

    public BookingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        IUnitOfWork unitOfWork,
        IOptions<BookingOptions> options)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
        _maxActiveBookingsPerUser = options.Value.MaxActiveBookingsPerUser;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            // ВАЖНО: отслеживаемая сущность, чтобы изменение AvailableSeats сохранилось.
            var evt = await _eventRepository.GetByIdTrackedAsync(eventId, cancellationToken);

            if (evt is null)
                throw new NotFoundException("Event not found.");

            if (evt.StartAt <= DateTime.UtcNow)
                throw new PastEventBookingException();

            if (await _bookingRepository.CountActiveByUserAsync(userId, cancellationToken) >= _maxActiveBookingsPerUser)
                throw new ActiveBookingsLimitExceededException();

            if (!evt.TryReserveSeats(1))
                throw new NoAvailableSeatsException();

            var booking = Booking.Create(eventId, userId);

            _bookingRepository.Add(booking);

            // Один SaveChangesAsync сохранит и бронь, и изменение AvailableSeats у evt (оба отслеживаются одним контекстом).
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        => _bookingRepository.GetByIdAsync(bookingId, cancellationToken);

    public async Task<bool> CancelBookingAsync(
        Guid bookingId,
        Guid callerId,
        UserRole callerRole,
        CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

            if (booking is null)
                return false;

            if (callerRole != UserRole.Admin && booking.UserId != callerId)
                throw new ForbiddenOperationException("Нельзя отменить чужую бронь.");

            var evt = await _eventRepository.GetByIdTrackedAsync(booking.EventId, cancellationToken);

            if (evt is not null && evt.StartAt <= DateTime.UtcNow)
                throw new PastEventBookingException();

            var wasActive = booking.Status is BookingStatus.Pending or BookingStatus.Confirmed;

            booking.Cancel();

            if (wasActive)
            {
                evt?.ReleaseSeats(1);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public async Task<bool> DeleteBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

            if (booking is null)
                return false;

            var wasActive = booking.Status is BookingStatus.Pending or BookingStatus.Confirmed;

            if (wasActive)
            {
                var evt = await _eventRepository.GetByIdTrackedAsync(booking.EventId, cancellationToken);
                evt?.ReleaseSeats(1);
            }

            _bookingRepository.Remove(booking);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public async Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
        => await _bookingRepository.GetPendingAsync(cancellationToken);

    public async Task<bool> TryUpdateBookingAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        var existing = await _bookingRepository.GetByIdTrackedAsync(booking.Id, cancellationToken);

        if (existing is null)
            return false;

        // CreatedAt не меняем
        existing.Status = booking.Status;
        existing.ProcessedAt = booking.ProcessedAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryProcessPendingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

            if (booking is null)
                return false;

            if (booking.Status != BookingStatus.Pending)
                return false;

            booking.Confirm();

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public async Task<bool> TryRejectPendingAsync(
        Guid bookingId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var booking = await _bookingRepository.GetByIdTrackedAsync(bookingId, cancellationToken);

            if (booking is null)
                return false;

            if (booking.Status != BookingStatus.Pending)
                return false;

            // событие могло быть удалено — тогда место вернуть некуда
            var evt = await _eventRepository.GetByIdTrackedAsync(booking.EventId, cancellationToken);

            if (evt is not null)
            {
                evt.ReleaseSeats(1);
            }

            booking.Reject();

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }
}
