using Contracts;

namespace Bookings.Application.Interfaces;

/// <summary>
/// Абстракция публикации события подтверждения брони (реализация — Kafka в Infrastructure).
/// </summary>
public interface IBookingConfirmedPublisher
{
    Task PublishAsync(BookingConfirmedEvent bookingConfirmedEvent, CancellationToken cancellationToken = default);
}
