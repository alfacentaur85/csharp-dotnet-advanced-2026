using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Dtos;

/// <summary>
/// DTO для создания/обновления мероприятия.
/// </summary>
public class EventCreateUpdateDto : IValidatableObject
{
    /// <summary>
    /// Заголовок мероприятие.
    /// </summary>
    [Required(ErrorMessage = "Title обязателен.")]
    [MinLength(1, ErrorMessage = "Title не может быть пустым.")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание мероприятия.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Дата и время начала мероприятия.
    /// </summary>
    [Required(ErrorMessage = "StartAt обязателен.")]
    public DateTime StartAt { get; set; }

    /// <summary>
    /// Дата и время окончания мероприятия.
    /// </summary>
    [Required(ErrorMessage = "EndAt обязателен.")]
    public DateTime EndAt { get; set; }

    /// <summary>
    /// Кросс-полевая валидация: EndAt должен быть позже StartAt.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
        {
            yield return new ValidationResult(
                "EndAt должен быть позже StartAt.",
                new[] { nameof(EndAt), nameof(StartAt) });
        }
    }
}