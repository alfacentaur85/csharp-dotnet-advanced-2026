using EventServiceApi.Dto;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;

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
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Дата начала события не может быть больше даты окончания события");

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
        query = query.OrderBy(e => e.StartAt).ThenBy(e => e.Id);

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
    public Event Create(EventCreateDto dto)
    {
        var evt = Event.Create(
                title: dto.Title,
                description: dto.Description,
                startAt: dto.StartAt,
                endAt: dto.EndAt,
                totalSeats: dto.TotalSeats);

        while (!_storage.TryAdd(evt.Id, evt))
            evt.Id = Guid.NewGuid();

        return evt;
    }

    /// <inheritdoc />
    public bool Update(Guid id, EventUpdateDto dto)
    {
        if (!_storage.TryGetValue(id, out var existing))
            return false;

        var (start, end) = ValidateDates(dto.StartAt, dto.EndAt);

        // Сколько мест уже занято по текущему состоянию
        var occupied = existing.TotalSeats - existing.AvailableSeats;
        if (occupied < 0) occupied = 0; // защита от неконсистентности

        var newTotalSeats = existing.TotalSeats;
        var newAvailableSeats = existing.AvailableSeats;

        if (dto.TotalSeats.HasValue)
        {
            newTotalSeats = dto.TotalSeats.Value;

            if (newTotalSeats <= 0)
                throw new ValidationException("TotalSeats должен быть больше нуля.");

            if (newTotalSeats < occupied)
                throw new ValidationException("Нельзя уменьшить TotalSeats ниже количества уже занятых мест.");

            newAvailableSeats = newTotalSeats - occupied;
        }

        var updated = new Event
        {
            Id = id,
            Title = dto.Title,
            Description = dto.Description,
            StartAt = start,
            EndAt = end,
            TotalSeats = newTotalSeats,
            AvailableSeats = newAvailableSeats
        };

        _storage[id] = updated;
        return true;
    }

    /// <inheritdoc />
    public bool Delete(Guid id)
        => _storage.TryRemove(id, out _);

    /// <inheritdoc />
    public bool TryUpdate(Event evt)
    {
        if (evt is null) throw new ArgumentNullException(nameof(evt));

        if (!_storage.ContainsKey(evt.Id))
            return false;

        _storage[evt.Id] = evt;
        return true;
    }

    private static (DateTime start, DateTime end) ValidateDates(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ValidationException("Дата окончания должна быть позже даты начала.");

        return (start, end);
    }
}