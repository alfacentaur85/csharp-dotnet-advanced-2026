using EventServiceApi.Models;

namespace EventServiceApi.Interfaces;

/// <summary>
/// Интерфейс сервиса для работы с мероприятиями.
/// </summary>
public interface IEventService
{
    /// <summary>
    /// Получить список всех мероприятий.
    /// </summary>
    IReadOnlyCollection<Event> GetAll();

    /// <summary>
    /// Получить мероприятие по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    /// <returns>Мероприятие или null, если не найдено.</returns>
    Event? GetById(Guid id);

    /// <summary>
    /// Создать новое мероприятие.
    /// </summary>
    /// <param name="evt">Мероприятие.</param>
    /// <returns>Созданное мероприятие.</returns>
    Event Create(Event evt);

    /// <summary>
    /// Полностью обновить мероприятие по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    /// <param name="evt">Новые данные мероприятия (Id игнорируется).</param>
    /// <returns>True если обновлено, иначе false.</returns>
    bool Update(Guid id, Event evt);

    /// <summary>
    /// Удалить мероприятие по идентификатору.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    /// <returns>True если удалено, иначе false.</returns>
    bool Delete(Guid id);
}