namespace Events.Application.Caching;

/// <summary>
/// Единые ключи кеша для мероприятий, чтобы запись и инвалидация всегда указывали на один и тот же ключ.
/// </summary>
public static class EventCacheKeys
{
    /// <summary>Ключ кеша отдельного события по идентификатору.</summary>
    public static string EventById(Guid id) => $"event:{id}";

    /// <summary>Ключ кеша списка топ-10 событий по проценту проданных мест.</summary>
    public const string Top10 = "events:top10";
}
