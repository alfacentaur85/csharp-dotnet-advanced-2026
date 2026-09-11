using Bookings.Application.Dto;
using Bookings.Application.Interfaces;
using Bookings.Application.Mappings;
using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Bookings.Api.Controllers;

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
    /// Создать бронь на событие. EventId передаётся в теле запроса
    /// (сервис Bookings не имеет собственного маршрута через Events).
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponseDto>> Create(
        [FromBody] BookingCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();

        var booking = await _bookingService.CreateBookingAsync(dto.EventId, userId, cancellationToken);

        return AcceptedAtAction(
            actionName: nameof(GetById),
            routeValues: new { id = booking.Id },
            value: booking.ToResponseDto());
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
