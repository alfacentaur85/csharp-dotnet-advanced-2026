using Users.Domain.Entities;

namespace Users.Application.Interfaces;

/// <summary>
/// Репозиторий для доступа к данным пользователей.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Получить пользователя по логину без отслеживания изменений (для чтения).
    /// </summary>
    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить пользователя по идентификатору без отслеживания изменений (для чтения).
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавить нового пользователя в контекст.
    /// </summary>
    void Add(User user);
}
