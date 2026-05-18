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
    public IReadOnlyCollection<Event> GetAll()
        => _storage.Values
            .OrderBy(e => e.StartAt)
            .ToList();

    /// <inheritdoc />
    public Event? GetById(Guid id)
        => _storage.TryGetValue(id, out var evt) ? evt : null;

    /// <inheritdoc />
    public Event Create(EventCreateUpdateDto dto)
    {
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

        if (dto.StartAt == null || dto.EndAt == null)
            throw new ArgumentException("Дата начала и окончания обязательны.");

        var evt = new Event
        {
            Id = id,
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt.Value,
            EndAt = dto.EndAt.Value
        };

        _storage[id] = evt;
        return true;
    }

    /// <inheritdoc />
    public bool Delete(Guid id)
        => _storage.TryRemove(id, out _);
}