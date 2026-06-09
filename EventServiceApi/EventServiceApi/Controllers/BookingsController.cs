using EventServiceApi.Dto;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventServiceApi.Controllers;

/// <summary>
/// REST API для управления бронированиями.
/// </summary>
[ApiController]
[Route("bookings")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    private static BookingResponseDto ToResponseDto(Booking booking) => new()
    {
        Id = booking.Id,
        EventId = booking.EventId,
        Status = booking.Status
    };

    /// <summary>
    /// Получить бронь по id.
    /// </summary>
    /// <param name="id">Идентификатор брони.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> GetById(Guid id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);
        if (booking is null)
            return NotFound();

        return Ok(ToResponseDto(booking));
    }
}