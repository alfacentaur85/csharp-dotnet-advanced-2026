using EventService.Application.Dto;
using EventService.Application.Interfaces;
using EventService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Controllers;

/// <summary>
/// REST API для регистрации и входа пользователей.
/// </summary>
[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Регистрирует нового пользователя и возвращает JWT-токен.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterRequestDto dto, CancellationToken cancellationToken = default)
    {
        var role = UserRole.User;
        if (!string.IsNullOrWhiteSpace(dto.Role) && !Enum.TryParse(dto.Role, ignoreCase: true, out role))
            throw new ValidationException("Недопустимое значение role.");

        var result = await _authService.RegisterAsync(dto.Login, dto.Password, role, cancellationToken);

        return CreatedAtAction(nameof(UsersController.GetById), "Users", new { id = result.UserId }, result);
    }

    /// <summary>
    /// Проверяет логин и пароль и возвращает JWT-токен.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var result = await _authService.LoginAsync(dto.Login, dto.Password, cancellationToken);

        return Ok(result);
    }
}
