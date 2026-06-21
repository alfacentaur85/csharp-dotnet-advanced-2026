using EventServiceApi.Models;

namespace EventServiceApi.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default);

    // для фоновой обработки
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default);
    Task<bool> TryUpdateBookingAsync(Booking booking, CancellationToken cancellationToken = default);
    Task<bool> TryProcessPendingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<bool> TryRejectPendingAsync(Guid bookingId, string? reason = null, CancellationToken cancellationToken = default);
}