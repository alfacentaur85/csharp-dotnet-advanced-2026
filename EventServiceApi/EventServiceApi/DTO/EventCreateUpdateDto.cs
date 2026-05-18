using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Dto;

/// <summary>
/// DTO для создания/обновления мероприятия.
/// </summary>
public class EventCreateUpdateDto : IValidatableObject
{
    /// <summary>
    /// Заголовок мероприятия.
    /// </summary>
    [Required(ErrorMessage = "Заголовок обязателен.")]
    [MinLength(1, ErrorMessage = "Заголовок не может быть пустым.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание мероприятия.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Дата и время начала мероприятия.
    /// </summary>
    [Required(ErrorMessage = "Дата начала обязательна.")]
    public DateTime? StartAt { get; set; }

    /// <summary>
    /// Дата и время окончания мероприятия.
    /// </summary>
    [Required(ErrorMessage = "Дата окончания обязательна.")]
    public DateTime? EndAt { get; set; }

    /// <summary>
    /// Кросс-полевая валидация: EndAt должен быть позже StartAt.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartAt == null || EndAt == null)
            yield break;

        if (EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "Дата окончания должна быть позже даты начала.",
                new[] { nameof(EndAt), nameof(StartAt) });
        }
    }
}