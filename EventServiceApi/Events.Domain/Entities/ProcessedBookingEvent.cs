namespace Events.Domain.Entities;

/// <summary>
/// Маркер обработанного события BookingConfirmed (инбокс для идемпотентности потребителя Kafka).
/// Наличие строки с данным BookingId означает, что бронирование уже учтено в AvailableSeats
/// и повторная доставка того же сообщения должна быть проигнорирована.
/// </summary>
public class ProcessedBookingEvent
{
    public Guid BookingId { get; set; }

    public Guid EventId { get; set; }

    public DateTime ProcessedAt { get; set; }
}
