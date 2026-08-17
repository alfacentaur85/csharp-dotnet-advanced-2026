using EventService.Application.Dto;
using EventService.Domain.Enums;

namespace EventService.Application.Interfaces;

/// <summary>
/// Сервис регистрации и входа пользователей.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Регистрирует нового пользователя и возвращает JWT-токен.
    /// </summary>
    /// <exception cref="Domain.Exceptions.LoginAlreadyExistsException">
    /// Выбрасывается, если пользователь с таким логином уже существует.
    /// </exception>
    Task<AuthResponseDto> RegisterAsync(string login, string password, UserRole role = UserRole.User, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверяет логин и пароль и возвращает JWT-токен.
    /// </summary>
    /// <exception cref="Domain.Exceptions.InvalidCredentialsException">
    /// Выбрасывается, если логин не найден или пароль неверен.
    /// </exception>
    Task<AuthResponseDto> LoginAsync(string login, string password, CancellationToken cancellationToken = default);
}
