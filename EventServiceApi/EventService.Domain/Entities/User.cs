using System.ComponentModel.DataAnnotations;
using EventService.Domain.Enums;

namespace EventService.Domain.Entities;

/// <summary>
/// Доменная модель пользователя.
/// </summary>
public class User
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
    /// Хеш пароля пользователя.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Роль пользователя.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Навигационное свойство EF Core: список броней пользователя.
    /// </summary>
    public List<Booking> Bookings { get; private set; } = [];

    /// <summary>
    /// Фабричный метод создания пользователя.
    /// Гарантирует доменные инварианты: логин и хеш пароля заданы.
    /// </summary>
    /// <param name="login">Логин пользователя (не пустой).</param>
    /// <param name="passwordHash">Хеш пароля (не пустой).</param>
    /// <param name="role">Роль пользователя (по умолчанию User).</param>
    /// <returns>Новый экземпляр <see cref="User"/>.</returns>
    /// <exception cref="ValidationException">
    /// Выбрасывается, если login или passwordHash пустые.
    /// </exception>
    public static User Create(
        string login,
        string passwordHash,
        UserRole role = UserRole.User)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ValidationException("Логин обязателен.");

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ValidationException("Хеш пароля обязателен.");

        return new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            PasswordHash = passwordHash,
            Role = role
        };
    }
}
