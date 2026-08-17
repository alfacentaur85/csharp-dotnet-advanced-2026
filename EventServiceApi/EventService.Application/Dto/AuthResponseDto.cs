namespace EventService.Application.Dto;

/// <summary>
/// DTO с JWT-токеном, возвращаемым после регистрации или входа.
/// </summary>
public class AuthResponseDto
{
    /// <summary>
    /// Идентификатор пользователя.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Токен пользователя.
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
