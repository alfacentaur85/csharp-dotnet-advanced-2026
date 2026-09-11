using Bookings.Domain.Entities;
using Bookings.Domain.Enums;

namespace Bookings.Application.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отменяет бронь. Пользователь может отменить только свою бронь, администратор — любую.
    /// </summary>
    /// <returns>false, если брони с таким id не существует.</returns>
    Task<bool> CancelBookingAsync(Guid bookingId, Guid callerId, UserRole callerRole, CancellationToken cancellationToken = default);

    /// <summary>
    /// Безвозвратно удаляет бронь (только для администратора).
    /// </summary>
    /// <returns>false, если брони с таким id не существует.</returns>
    Task<bool> DeleteBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    // для фоновой обработки
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task<bool> TryUpdateBookingAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<bool> TryProcessPendingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<bool> TryRejectPendingAsync(Guid bookingId, string? reason = null, CancellationToken cancellationToken = default);
}
