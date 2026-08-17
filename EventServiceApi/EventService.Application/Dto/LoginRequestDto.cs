using System.ComponentModel.DataAnnotations;

namespace EventService.Application.Dto;

/// <summary>
/// DTO для входа пользователя.
/// </summary>
public class LoginRequestDto
{
    [Required(ErrorMessage = "Логин обязателен.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен.")]
    public string Password { get; set; } = string.Empty;
}
