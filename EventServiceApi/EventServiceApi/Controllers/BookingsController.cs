using EventService.Application.Dto;
using EventService.Domain.Enums;
using EventService.Domain.Exceptions;
using EventService.Application.Interfaces;
using EventService.Application.Mappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
    [Authorize]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> GetById(Guid id)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id);
        if (booking is null)
            throw new NotFoundException("Booking not found.");

        var callerId = GetUserId();
        var callerRole = GetUserRole();

        if (booking.UserId != callerId && callerRole != UserRole.Admin)
            throw new ForbiddenOperationException("Нельзя просматривать чужую бронь.");

        return Ok(booking.ToResponseDto());
    }

    /// <summary>
    /// Отменяет бронь. Пользователь может отменить только свою бронь, администратор — любую.
    /// </summary>
    /// <param name="id">Идентификатор брони.</param>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken = default)
    {
        var callerId = GetUserId();
        var callerRole = GetUserRole();

        var cancelled = await _bookingService.CancelBookingAsync(id, callerId, callerRole, cancellationToken);
        if (!cancelled)
            throw new NotFoundException("Booking not found.");

        return NoContent();
    }

    /// <summary>
    /// Безвозвратно удаляет бронь. Доступно только администраторам.
    /// </summary>
    /// <param name="id">Идентификатор брони.</param>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await _bookingService.DeleteBookingAsync(id, cancellationToken);
        if (!deleted)
            throw new NotFoundException("Booking not found.");

        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(claim, out var userId))
            throw new UnauthorizedAccessException("User id claim is missing or invalid.");

        return userId;
    }

    private UserRole GetUserRole()
    {
        var claim = User.FindFirstValue(ClaimTypes.Role);

        if (!Enum.TryParse<UserRole>(claim, out var role))
            throw new UnauthorizedAccessException("User role claim is missing or invalid.");

        return role;
    }
}