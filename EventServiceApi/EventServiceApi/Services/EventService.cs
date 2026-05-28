using EventServiceApi.Dto;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using System.Collections.Concurrent;

namespace EventServiceApi.Services;

/// <summary>
/// Реализация сервиса мероприятий.
/// </summary>
public class EventService : IEventService
{
    private readonly ConcurrentDictionary<Guid, Event> _storage = new();

    /// <inheritdoc />
    public PaginatedResult<Event> GetAll(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10)
    {
        if (page < 1) throw new ArgumentException("page должен быть >= 1");
        if (pageSize < 1) throw new ArgumentException("pageSize должен быть >= 1");

        IEnumerable<Event> query = _storage.Values;

        if (!string.IsNullOrWhiteSpace(title))
        {
            var t = title.Trim();
            query = query.Where(e => e.Title != null &&
                                     e.Title.Contains(t, StringComparison.OrdinalIgnoreCase));
        }

        if (from.HasValue)
            query = query.Where(e => e.StartAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.EndAt <= to.Value);

        // Важно: сначала считаем общее количество после фильтрации
        var totalCount = query.Count();

        // Стабильная сортировка перед пагинацией
        query = query.OrderBy(e => e.StartAt);

        var items = query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Page = page,
            Count = items.Count,
            Items = items
        };
    }

    /// <inheritdoc />
    public Event? GetById(Guid id)
        => _storage.TryGetValue(id, out var evt) ? evt : null;

    /// <inheritdoc />
    public Event Create(EventCreateUpdateDto dto)
    {
        ValidateDates(dto);

        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt!.Value,
            EndAt = dto.EndAt!.Value
        };

        while (!_storage.TryAdd(evt.Id, evt))
            evt.Id = Guid.NewGuid();

        return evt;
    }

    /// <inheritdoc />
    public bool Update(Guid id, EventCreateUpdateDto dto)
    {
        if (!_storage.ContainsKey(id))
            return false;

        var (start, end) = ValidateDates(dto);

        var evt = new Event
        {
            Id = id,
            Title = dto.Title,
            Description = dto.Description,
            StartAt = start,
            EndAt = end
        };

        _storage[id] = evt;

        return true;
    }

    /// <inheritdoc />
    public bool Delete(Guid id)
        => _storage.TryRemove(id, out _);

    private static (DateTime start, DateTime end) ValidateDates(EventCreateUpdateDto dto)
    {
        if (dto.StartAt is null || dto.EndAt is null)
            throw new ArgumentException("Дата начала и окончания обязательны.");

        var start = dto.StartAt.Value;
        var end = dto.EndAt.Value;

        if (end <= start)
            throw new ArgumentException("Дата окончания должна быть позже даты начала.");

        return (start, end);
    }
}