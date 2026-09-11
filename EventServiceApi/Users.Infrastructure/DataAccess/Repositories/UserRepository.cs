using Users.Application.Interfaces;
using Users.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Users.Infrastructure.DataAccess.Repositories;

/// <summary>
/// Реализация репозитория пользователей (EF Core).
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly UsersDbContext _context;

    public UserRepository(UsersDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
        => _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public void Add(User user) => _context.Users.Add(user);
}
