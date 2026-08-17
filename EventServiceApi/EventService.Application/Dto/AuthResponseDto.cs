namespace EventService.Application.Dto;

/// <summary>
/// DTO с JWT-токеном, возвращаемым после регистрации или входа.
/// </summary>
public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
}
