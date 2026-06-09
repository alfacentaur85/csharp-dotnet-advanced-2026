using EventServiceApi.Models;

namespace EventServiceApi.Interfaces;

public interface IBookingService
{
    Task<Booking> CreateBookingAsync(Guid eventId);
    Task<Booking?> GetBookingByIdAsync(Guid bookingId);

    // для фоновой обработки
    Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync();
    Task<bool> TryUpdateBookingAsync(Booking booking);
}