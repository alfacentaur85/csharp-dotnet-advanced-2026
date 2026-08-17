using EventService.Domain.Entities;

namespace EventService.Application.Interfaces;

/// <summary>
/// Сервис для чтения данных пользователей.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Получить пользователя по идентификатору.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
