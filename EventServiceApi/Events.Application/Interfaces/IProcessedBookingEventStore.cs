namespace Events.Application.Interfaces;

/// <summary>
/// Инбокс идемпотентности для событий BookingConfirmed.
/// Запись добавляется в контекст, но не сохраняется — сохранение выполняется вместе
/// с изменением Event в рамках одной транзакции (см. IUnitOfWork.SaveChangesAsync).
/// </summary>
public interface IProcessedBookingEventStore
{
    void MarkProcessed(Guid bookingId, Guid eventId);
}
