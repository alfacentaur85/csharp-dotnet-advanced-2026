namespace EventServiceApi.Dto;

public class PaginatedResult<T>
{
    /// <summary>Общее количество элементов (после фильтрации, до пагинации).</summary>
    public int TotalCount { get; set; }

    /// <summary>Текущая страница (1-based).</summary>
    public int Page { get; set; }

    /// <summary>Количество элементов на текущей странице.</summary>
    public int Count { get; set; }

    /// <summary>Элементы текущей страницы.</summary>
    public IReadOnlyCollection<T> Items { get; set; } = Array.Empty<T>();
}