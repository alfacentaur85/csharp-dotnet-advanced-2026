using EventService.Domain.Entities;

namespace EventService.Application.Interfaces;

/// <summary>
/// Репозиторий для доступа к данным мероприятий.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Получить страницу мероприятий с фильтрацией (без отслеживания изменений).
    /// </summary>
    Task<(IReadOnlyList<Event> Items, int TotalCount)> GetPagedAsync(
        string? title,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить мероприятие по идентификатору без отслеживания изменений (для чтения).
    /// </summary>
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить мероприятие по идентификатору с отслеживанием изменений (для изменения/удаления).
    /// </summary>
    Task<Event?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавить новое мероприятие в контекст.
    /// </summary>
    void Add(Event evt);

    /// <summary>
    /// Удалить мероприятие из контекста.
    /// </summary>
    void Remove(Event evt);
}
