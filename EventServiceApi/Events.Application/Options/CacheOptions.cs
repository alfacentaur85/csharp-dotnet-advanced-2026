namespace Events.Application.Options;

/// <summary>
/// Настройки кеша (Redis) для сервиса событий.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Строка подключения к Redis (StackExchange.Redis формат, например "redis:6379").
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// TTL записи одного события (ключ event:{id}) в секундах.
    /// </summary>
    public int EventTtlSeconds { get; set; } = 300;

    /// <summary>
    /// TTL списка топ-10 событий (ключ events:top10) в секундах.
    /// </summary>
    public int TopEventsTtlSeconds { get; set; } = 600;
}
