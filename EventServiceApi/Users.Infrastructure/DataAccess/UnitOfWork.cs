using Users.Application.Interfaces;

namespace Users.Infrastructure.DataAccess;

/// <summary>
/// Реализация IUnitOfWork поверх UsersDbContext.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly UsersDbContext _context;

    public UnitOfWork(UsersDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
