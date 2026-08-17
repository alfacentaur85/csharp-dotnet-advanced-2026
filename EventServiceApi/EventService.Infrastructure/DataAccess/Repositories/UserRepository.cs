using EventService.Application.Interfaces;
using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.DataAccess.Repositories;

/// <summary>
/// Реализация репозитория пользователей (EF Core).
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken = default)
        => _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    public void Add(User user) => _context.Users.Add(user);
}
