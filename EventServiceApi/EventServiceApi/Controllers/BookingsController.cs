using EventServiceApi.Dto;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Mappings;
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

    /// <summary>
    /// Получить бронь по id.
    /// </summary>
    /// <param name="id">Идентификатор брони.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> GetById(Guid id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);
        if (booking is null)
            throw new NotFoundException("Booking not found.");

        return Ok(booking.ToResponseDto());
    }
}