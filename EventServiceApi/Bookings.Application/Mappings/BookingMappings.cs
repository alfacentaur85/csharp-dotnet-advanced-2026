using Bookings.Application.Dto;
using Bookings.Domain.Entities;

namespace Bookings.Application.Mappings;

public static class BookingMappings
{
    public static BookingResponseDto ToResponseDto(this Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        UserId = booking.UserId,
        Status = booking.Status,
        CreatedAt = booking.CreatedAt,
        ProcessedAt = booking.ProcessedAt
    };
}
