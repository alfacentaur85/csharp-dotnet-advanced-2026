// Models/Booking.cs
using System.ComponentModel.DataAnnotations;
using EventServiceApi.Enums;

namespace EventServiceApi.Models;

/// <summary>
/// Доменная модель бронирования.
/// </summary>
public class Booking
{
    /// <summary>Уникальный идентификатор брони.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>Идентификатор события, к которому относится бронь.</summary>
    [Required]
    public Guid EventId { get; set; }

    /// <summary>Текущий статус брони.</summary>
    [Required]
    public BookingStatus Status { get; set; }

    /// <summary>Дата и время создания брони.</summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>Дата и время обработки брони (опционально).</summary>
    public DateTime? ProcessedAt { get; set; }

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
    /// Используется, если бронь не может быть подтверждена (например, событие удалено или произошла ошибка обработки).
    /// </remarks>
    public void Reject()
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Навигационное свойство EF Core: событие, к которому относится бронь.
    /// </summary>
    public Event Event { get; private set; } = null!;
}