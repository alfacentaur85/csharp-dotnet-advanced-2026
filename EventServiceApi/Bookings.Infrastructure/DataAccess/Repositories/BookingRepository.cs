using Bookings.Application.Interfaces;
using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Bookings.Infrastructure.DataAccess.Repositories;

/// <summary>
/// Реализация репозитория броней (EF Core).
/// </summary>
public sealed class BookingRepository : IBookingRepository
{
    private readonly BookingsDbContext _context;

    public BookingRepository(BookingsDbContext context)
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

    public void Remove(Booking booking) => _context.Bookings.Remove(booking);

    public Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => _context.Bookings.CountAsync(
            b => b.UserId == userId && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed),
            cancellationToken);
}
