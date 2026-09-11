namespace Bookings.Application.Options;

/// <summary>
/// Настройки бизнес-правил бронирования.
/// </summary>
public sealed class BookingOptions
{
    /// <summary>
    /// Максимальное количество активных (Pending/Confirmed) броней на одного пользователя.
    /// </summary>
    public int MaxActiveBookingsPerUser { get; set; } = 10;
}
