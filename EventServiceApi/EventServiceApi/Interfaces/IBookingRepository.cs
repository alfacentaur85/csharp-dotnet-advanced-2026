using EventServiceApi.Models;

namespace EventServiceApi.Interfaces;

/// <summary>
/// Репозиторий для доступа к данным броней.
/// </summary>
public interface IBookingRepository
{
    /// <summary>
    /// Получить бронь по идентификатору без отслеживания изменений (для чтения).
    /// </summary>
    Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить бронь по идентификатору с отслеживанием изменений (для изменения).
    /// </summary>
    Task<Booking?> GetByIdTrackedAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Получить все брони в статусе Pending, упорядоченные по дате создания.
    /// </summary>
    Task<IReadOnlyCollection<Booking>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Добавить новую бронь в контекст.
    /// </summary>
    void Add(Booking booking);
}
