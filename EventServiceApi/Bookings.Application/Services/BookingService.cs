using Bookings.Application.Interfaces;
using Bookings.Application.Options;
using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;
using Contracts;
using Microsoft.Extensions.Options;

namespace Bookings.Application.Services;

/// <summary>
/// Реализация сервиса броней (бизнес-логика и конкурентность; доступ к данным — через репозитории).
/// Bookings — единственный владелец своих данных: сервис не знает о местах события,
/// подтверждение брони асинхронно публикуется в Kafka и потребляется сервисом Events.
/// </summary>
public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBookingConfirmedPublisher _publisher;
    private readonly int _maxActiveBookingsPerUser;

    private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

    public BookingService(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IBookingConfirmedPublisher publisher,
        IOptions<BookingOptions> options)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
        _maxActiveBookingsPerUser = options.Value.MaxActiveBookingsPerUser;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (await _bookingRepository.CountActiveByUserAsync(userId, cancellationToken) >= _maxActiveBookingsPerUser)
                throw new ActiveBookingsLimitExceededException();

            var booking = Booking.Create(eventId, userId);

            _bookingRepository.Add(booking);

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

            booking.Cancel();

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

            // Сначала сохраняем в БД, и только после успешного сохранения публикуем в Kafka.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _publisher.PublishAsync(
                new BookingConfirmedEvent(
                    booking.Id,
                    booking.EventId,
                    booking.UserId,
                    SeatsCount: 1,
                    booking.ProcessedAt!.Value),
                cancellationToken);

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
