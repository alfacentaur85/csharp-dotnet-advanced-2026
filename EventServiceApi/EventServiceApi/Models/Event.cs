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
    public DateTime? StartAt { get; set; }

    /// <summary>
    /// Дата и время окончания события.
    /// </summary>
    [Required]
    public DateTime? EndAt { get; set; }
}