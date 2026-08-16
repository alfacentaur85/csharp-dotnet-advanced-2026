using EventService.Application.Interfaces;
using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventService.Infrastructure.DataAccess.Repositories;

/// <summary>
/// Реализация репозитория мероприятий (EF Core).
/// </summary>
public sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _context;

    public EventRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Event> query = _context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim().ToLower();

            query = query.Where(e => e.Title.ToLower().Contains(t));
        }

        if (from.HasValue)
            query = query.Where(e => e.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.EndAt <= to.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.StartAt).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(Event evt) => _context.Events.Add(evt);

    public void Remove(Event evt) => _context.Events.Remove(evt);
}
