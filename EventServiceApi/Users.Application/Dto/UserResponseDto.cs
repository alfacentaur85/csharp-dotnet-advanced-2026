namespace Users.Application.Dto;

/// <summary>
/// DTO для возврата информации о пользователе.
/// </summary>
public class UserResponseDto
{
    /// <summary>
    /// Идентификатор пользователя.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Логин пользователя.
    /// </summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>
    /// Роль пользователя.
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
