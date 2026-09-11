using System.ComponentModel.DataAnnotations;

namespace Bookings.Application.Dto;

/// <summary>
/// DTO для создания брони. EventId передаётся в теле запроса,
/// т.к. у сервиса Bookings нет собственного маршрута через Events.
/// </summary>
public class BookingCreateDto
{
    [Required]
    public Guid EventId { get; set; }
}
