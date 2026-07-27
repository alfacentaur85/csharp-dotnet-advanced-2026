using EventServiceApi.Enums;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;

namespace EventServiceApi.Services;

/// <summary>
/// Реализация сервиса броней (бизнес-логика и конкурентность; доступ к данным — через репозитории).
/// </summary>
public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

    public BookingService(
        IBookingRepository bookingRepository,
        IEventRepository eventRepository,
        IUnitOfWork unitOfWork)
    {
        _bookingRepository = bookingRepository;
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            // ВАЖНО: отслеживаемая сущность, чтобы изменение AvailableSeats сохранилось.
            var evt = await _eventRepository.GetByIdTrackedAsync(eventId, cancellationToken);

            if (evt is null)
                throw new NotFoundException("Event not found.");

            if (!evt.TryReserveSeats(1))
                throw new NoAvailableSeatsException();

            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                Status = BookingStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = null
            };

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
