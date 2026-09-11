using Events.Application.Interfaces;

namespace Events.Infrastructure.DataAccess;

/// <summary>
/// Реализация IUnitOfWork поверх EventsDbContext.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly EventsDbContext _context;

    public UnitOfWork(EventsDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
