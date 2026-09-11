using System.ComponentModel.DataAnnotations;
using Bookings.Domain.Enums;

namespace Bookings.Domain.Entities;

/// <summary>
/// Доменная модель бронирования.
/// Bookings — единственный владелец этих данных; EventId/UserId — плоские ссылки
/// (без навигационных свойств и FK-ограничений), т.к. Events и Users — отдельные сервисы/БД.
/// </summary>
public class Booking
{
    /// <summary>Уникальный идентификатор брони.</summary>
    public Guid Id { get; set; }

    /// <summary>Идентификатор события, к которому относится бронь.</summary>
    public Guid EventId { get; set; }

    /// <summary>Идентификатор пользователя, создавшего бронь.</summary>
    public Guid UserId { get; set; }

    /// <summary>Текущий статус брони.</summary>
    public BookingStatus Status { get; set; }

    /// <summary>Дата и время создания брони.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата и время обработки брони (опционально).</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Фабричный метод создания брони.
    /// Гарантирует доменные инварианты: указаны событие и пользователь.
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="userId">Идентификатор пользователя, создающего бронь.</param>
    /// <returns>Новый экземпляр <see cref="Booking"/> в статусе <see cref="BookingStatus.Pending"/>.</returns>
    /// <exception cref="ValidationException">
    /// Выбрасывается, если eventId или userId не заданы.
    /// </exception>
    public static Booking Create(Guid eventId, Guid userId)
    {
        if (eventId == Guid.Empty)
            throw new ValidationException("EventId обязателен.");

        if (userId == Guid.Empty)
            throw new ValidationException("UserId обязателен.");

        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };
    }

    /// <summary>
    /// Переводит бронь в статус <see cref="BookingStatus.Confirmed"/> и устанавливает <see cref="ProcessedAt"/> (UTC).
    /// </summary>
    /// <remarks>
    /// Предполагается, что метод вызывается при обработке брони (обычно из состояния Pending).
    /// </remarks>
    public void Confirm()
    {
        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Переводит бронь в статус <see cref="BookingStatus.Rejected"/> и устанавливает <see cref="ProcessedAt"/> (UTC).
    /// </summary>
    /// <remarks>
    /// Используется, если бронь не может быть подтверждена (например, ошибка обработки).
    /// </remarks>
    public void Reject()
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Отменяет бронь: переводит в статус <see cref="BookingStatus.Cancelled"/> и устанавливает <see cref="ProcessedAt"/> (UTC).
    /// </summary>
    /// <exception cref="ValidationException">
    /// Выбрасывается, если бронь уже находится в терминальном статусе (Cancelled или Rejected) — защита от повторной отмены.
    /// </exception>
    public void Cancel()
    {
        if (Status is BookingStatus.Cancelled or BookingStatus.Rejected)
            throw new ValidationException("Бронь уже находится в финальном статусе и не может быть отменена повторно.");

        Status = BookingStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
    }
}
