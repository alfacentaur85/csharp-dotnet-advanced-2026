using EventServiceApi.Enums;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventServiceApi.DataAccess.Repositories;

/// <summary>
/// Реализация репозитория броней (EF Core).
/// </summary>
public sealed class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<Booking?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Booking>> GetPendingAsync(CancellationToken cancellationToken = default)
        => await _context.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

    public void Add(Booking booking) => _context.Bookings.Add(booking);
}
