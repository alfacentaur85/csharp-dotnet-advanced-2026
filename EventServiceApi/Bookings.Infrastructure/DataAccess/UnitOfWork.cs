using Bookings.Application.Interfaces;

namespace Bookings.Infrastructure.DataAccess;

/// <summary>
/// Реализация IUnitOfWork поверх BookingsDbContext.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly BookingsDbContext _context;

    public UnitOfWork(BookingsDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
