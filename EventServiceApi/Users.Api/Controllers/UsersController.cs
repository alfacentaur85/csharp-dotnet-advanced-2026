using Users.Application.Dto;
using Users.Domain.Enums;
using Users.Domain.Exceptions;
using Users.Application.Interfaces;
using Users.Application.Mappings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Users.Api.Controllers;

/// <summary>
/// REST API для чтения данных пользователей.
/// </summary>
[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Получить пользователя по id. Доступно самому пользователю или администратору.
    /// </summary>
    /// <param name="id">Идентификатор пользователя.</param>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponseDto>> GetById(Guid id)
    {
        var callerId = GetUserId();
        var callerRole = GetUserRole();

        if (id != callerId && callerRole != UserRole.Admin)
            throw new ForbiddenOperationException("Нельзя просматривать чужой профиль.");

        var user = await _userService.GetByIdAsync(id);
        if (user is null)
            throw new NotFoundException("User not found.");

        return Ok(user.ToResponseDto());
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
