using EventService.Application.Interfaces;
using EventService.Domain.Entities;

namespace EventService.Application.Services;

/// <summary>
/// Реализация сервиса чтения данных пользователей.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _userRepository.GetByIdAsync(id, cancellationToken);
}
