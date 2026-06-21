using System.ComponentModel.DataAnnotations;
using EventServiceApi.DataAccess;
using EventServiceApi.Dto;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventServiceApi.Services;

/// <summary>
/// Реализация сервиса мероприятий (EF Core).
/// </summary>
public sealed class EventService : IEventService
{
    private readonly AppDbContext _context;

    public EventService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Дата начала события не может быть больше даты окончания события");

        if (page < 1) throw new ArgumentException("page должен быть >= 1");
        if (pageSize < 1) throw new ArgumentException("pageSize должен быть >= 1");

        IQueryable<Event> query = _context.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim();

            // Для PostgreSQL можно заменить на ILIKE через EF.Functions.ILike(...)
            query = query.Where(e => e.Title.Contains(t));
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

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Page = page,
            Count = items.Count,
            Items = items
        };
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Events.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<Event> CreateAsync(EventCreateDto dto, CancellationToken cancellationToken = default)
    {
        var evt = Event.Create(
            title: dto.Title,
            description: dto.Description,
            startAt: dto.StartAt,
            endAt: dto.EndAt,
            totalSeats: dto.TotalSeats);

        _context.Events.Add(evt);
        await _context.SaveChangesAsync(cancellationToken);

        return evt;
    }

    public async Task<bool> UpdateAsync(Guid id, EventUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (existing is null)
            return false;

        ValidateDates(dto.StartAt, dto.EndAt);

        // Сколько мест уже занято по текущему состоянию
        var occupied = existing.TotalSeats - existing.AvailableSeats;
        if (occupied < 0) occupied = 0;

        if (dto.TotalSeats.HasValue)
        {
            var newTotalSeats = dto.TotalSeats.Value;

            if (newTotalSeats <= 0)
                throw new ValidationException("TotalSeats должен быть больше нуля.");

            if (newTotalSeats < occupied)
                throw new ValidationException("Нельзя уменьшить TotalSeats ниже количества уже занятых мест.");

            existing.TotalSeats = newTotalSeats;
            existing.AvailableSeats = newTotalSeats - occupied;
        }

        existing.Title = dto.Title;
        existing.Description = dto.Description;
        existing.StartAt = dto.StartAt;
        existing.EndAt = dto.EndAt;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (existing is null)
            return false;

        _context.Events.Remove(existing);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateDates(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ValidationException("Дата окончания должна быть позже даты начала.");
    }
}