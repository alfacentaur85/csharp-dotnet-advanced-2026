using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Dto;

/// <summary>
/// DTO для обновления мероприятия.
/// </summary>
public class EventUpdateDto : IValidatableObject
{
    [Required, MinLength(1)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats должен быть >= 1.")]
    public int? TotalSeats { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
            yield return new ValidationResult("Дата окончания должна быть позже даты начала.",
                new[] { nameof(EndAt), nameof(StartAt) });
    }
}