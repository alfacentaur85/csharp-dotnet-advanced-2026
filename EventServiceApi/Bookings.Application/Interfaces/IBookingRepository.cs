using Bookings.Domain.Entities;

namespace Bookings.Application.Interfaces;

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

    /// <summary>
    /// Удалить бронь из контекста.
    /// </summary>
    void Remove(Booking booking);

    /// <summary>
    /// Количество активных (Pending/Confirmed) броней пользователя.
    /// </summary>
    Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
