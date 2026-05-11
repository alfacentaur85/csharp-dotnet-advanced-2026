using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using System.Collections.Concurrent;

namespace EventServiceApi.Services;

/// <summary>
/// In-memory (пока нет БД) реализация сервиса мероприятий.
/// </summary>
public class EventService : IEventService
{
    private readonly ConcurrentDictionary<Guid, Event> _storage = new();

    /// <inheritdoc />
    public IReadOnlyCollection<Event> GetAll()
        => _storage.Values
            .OrderBy(e => e.StartAt)
            .ToArray();

    /// <inheritdoc />
    public Event? GetById(Guid id)
        => _storage.TryGetValue(id, out var evt) ? evt : null;

    /// <inheritdoc />
    public Event Create(Event evt)
    {
        if (evt.Id == Guid.Empty)
            evt.Id = Guid.NewGuid();

        // Считаем, что Id уникален; при коллизии — генерируем новый
        while (!_storage.TryAdd(evt.Id, evt))
            evt.Id = Guid.NewGuid();

        return evt;
    }

    /// <inheritdoc />
    public bool Update(Guid id, Event evt)
    {
        if (!_storage.ContainsKey(id))
            return false;

        // Полная замена сущности, Id фиксируем по маршруту
        evt.Id = id;
        _storage[id] = evt;
        return true;
    }

    /// <inheritdoc />
    public bool Delete(Guid id)
        => _storage.TryRemove(id, out _);
}