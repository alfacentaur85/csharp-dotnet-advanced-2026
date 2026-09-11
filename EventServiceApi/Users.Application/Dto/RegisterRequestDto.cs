using System.ComponentModel.DataAnnotations;

namespace Users.Application.Dto;

/// <summary>
/// DTO для регистрации пользователя.
/// </summary>
public class RegisterRequestDto
{
    [Required(ErrorMessage = "Логин обязателен.")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Пароль обязателен.")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Необязательная роль пользователя ("User" по умолчанию, допустимо "Admin").
    /// </summary>
    public string? Role { get; set; }
}
