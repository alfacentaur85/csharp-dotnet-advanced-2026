using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Models;

/// <summary>
/// Доменная модель события.
/// </summary>
public class Event
{
    /// <summary>
    /// Идентификатор события.
    /// </summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>
    /// Заголовок события.
    /// </summary>
    [Required]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание события (опционально).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Дата и время начала события.
    /// </summary>
    [Required]
    public DateTime StartAt { get; set; }

    /// <summary>
    /// Дата и время окончания события.
    /// </summary>
    [Required]
    public DateTime EndAt { get; set; }

    /// <summary>
    /// Общее количество мест на событии.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue)]
    public int TotalSeats { get; set; }

    /// <summary>
    /// Текущее количество свободных мест.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int AvailableSeats { get; set; }

    /// лок для атомарного изменения AvailableSeats
    private readonly object _seatsLock = new();

    /// <summary>
    /// Пытается зарезервировать места на событие.
    /// </summary>
    /// <param name="count">Количество мест (по умолчанию 1).</param>
    /// <returns>
    /// false — если свободных мест недостаточно;
    /// true — если места были и AvailableSeats уменьшен на count.
    /// </returns>
    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count должен быть >= 1.");

        lock (_seatsLock)
        {
            if (AvailableSeats < count)
                return false;

            AvailableSeats -= count;
            return true;
        }
    }

    /// <summary>
    /// Освобождает места (увеличивает AvailableSeats), но не выше TotalSeats.
    /// </summary>
    /// <param name="count">Количество мест (по умолчанию 1).</param>
    public void ReleaseSeats(int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "count должен быть >= 1.");

        lock (_seatsLock)
        {
            // не даём выйти за пределы TotalSeats
            var newValue = AvailableSeats + count;
            AvailableSeats = newValue > TotalSeats ? TotalSeats : newValue;
        }
    }
}