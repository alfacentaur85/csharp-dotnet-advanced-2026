using EventServiceApi.Dto;
using EventServiceApi.Models;

namespace EventServiceApi.Interfaces;

/// <summary>
/// Интерфейс сервиса для работы с мероприятиями.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Получить список всех мероприятий (с фильтрацией и пагинацией).
    /// </summary>
    /// <param name="title">Поиск по названию (частичное совпадение, регистронезависимо).</param>
    /// <param name="from">События, которые начинаются не раньше указанной даты (StartAt >= from).</param>
    /// <param name="to">События, которые заканчиваются не позже указанной даты (EndAt &lt;= to).</param>
    /// <param name="page">Номер страницы (1-based).</param>
    /// <param name="pageSize">Размер страницы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить мероприятие по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Мероприятие или null, если не найдено.</returns>
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Создать новое мероприятие.
    /// </summary>
    /// <param name="evt">DTO для создания мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданное мероприятие.</returns>
    Task<Event> CreateAsync(EventCreateDto evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Полностью обновить мероприятие по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    /// <param name="evt">Новые данные мероприятия (Id игнорируется).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>True если обновлено, иначе false.</returns>
    Task<bool> UpdateAsync(Guid id, EventUpdateDto evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удалить мероприятие по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>True если удалено, иначе false.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}