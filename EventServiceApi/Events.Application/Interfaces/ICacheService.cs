namespace Events.Application.Interfaces;

/// <summary>
/// Абстракция кеша, скрывающая конкретную реализацию (Redis) от слоя Application.
/// </summary>
public interface ICacheService
{
    /// <summary>Получить значение по ключу. Возвращает default, если значения нет или кеш недоступен.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Сохранить значение по ключу с заданным временем жизни.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>Удалить значение по ключу.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
