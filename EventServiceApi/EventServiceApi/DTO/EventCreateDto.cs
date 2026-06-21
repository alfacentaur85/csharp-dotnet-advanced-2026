using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Dto;

/// <summary>
/// DTO для создания мероприятия.
/// </summary>
public class EventCreateDto : IValidatableObject
{
    [Required(ErrorMessage = "Заголовок обязателен.")]
    [MinLength(1, ErrorMessage = "Заголовок не может быть пустым.")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Дата начала обязательна.")]
    public DateTime StartAt { get; set; }

    [Required(ErrorMessage = "Дата окончания обязательна.")]
    public DateTime EndAt { get; set; }

    /// <summary>
    /// Общее количество мест (обязательно при создании).
    /// </summary>
    [Required(ErrorMessage = "TotalSeats обязателен.")]
    [Range(1, int.MaxValue, ErrorMessage = "TotalSeats должен быть >= 1.")]
    public int? TotalSeats { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "Дата окончания должна быть позже даты начала.",
                new[] { nameof(EndAt), nameof(StartAt) });
        }
    }
}