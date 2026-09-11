namespace Events.Application.Interfaces;

/// <summary>
/// Абстракция над сохранением изменений контекста БД,
/// позволяющая репозиториям разделять один и тот же контекст в рамках одной бизнес-операции.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
