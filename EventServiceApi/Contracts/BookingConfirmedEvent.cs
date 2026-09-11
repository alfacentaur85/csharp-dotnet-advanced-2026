namespace Contracts;

/// <summary>
/// Публичный контракт события подтверждения брони (топик <see cref="KafkaTopics.BookingConfirmed"/>).
/// Издаётся сервисом броней, потребляется сервисом событий.
/// </summary>
public sealed record BookingConfirmedEvent(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime ConfirmedAt);
