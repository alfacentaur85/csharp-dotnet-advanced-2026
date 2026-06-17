using EventServiceApi.Enums;

namespace EventServiceApi.Dto;

/// <summary>
/// DTO для возврата информации о бронировании.
/// </summary>
public class BookingResponseDto
{
    /// <summary>
    /// Идентификатор брони.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Идентификатор события, к которому относится бронь.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Текущий статус брони.
    /// </summary>
    public BookingStatus Status { get; set; }

    /// <summary>
    /// Дата/время создания брони (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Дата/время обработки брони (UTC), если обработана.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}