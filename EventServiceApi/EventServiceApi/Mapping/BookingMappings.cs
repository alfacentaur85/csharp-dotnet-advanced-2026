using EventServiceApi.Dto;
using EventServiceApi.Models;

namespace EventServiceApi.Mappings;

public static class BookingMappings
{
    public static BookingResponseDto ToResponseDto(this Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}