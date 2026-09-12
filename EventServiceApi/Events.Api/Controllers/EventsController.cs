using Events.Application.Dto;
using Events.Application.Interfaces;
using Events.Domain.Entities;
using Events.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Events.Api.Controllers;

/// <summary>
/// REST API для управления мероприятиями.
/// </summary>
[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    /// <summary>
    /// Конструктор контроллера мероприятий.
    /// </summary>
    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    private static EventResponseDto ToResponseDto(Event evt) => new()
    {
        Id = evt.Id,
        Title = evt.Title,
        Description = evt.Description,
        StartAt = evt.StartAt,
        EndAt = evt.EndAt,
        TotalSeats = evt.TotalSeats,
        AvailableSeats = evt.AvailableSeats
    };

    /// <summary>
    /// Получить список всех мероприятий (с фильтрацией).
    /// </summary>
    /// <param name="title">Поиск по названию (частичное совпадение, регистронезависимо).</param>
    /// <param name="from">События, которые начинаются не раньше указанной даты.</param>
    /// <param name="to">События, которые заканчиваются не позже указанной даты.</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<EventResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaginatedResult<EventResponseDto>>> GetAll(
        [FromQuery] string? title,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _eventService.GetAllAsync(
            title: title,
            from: from,
            to: to,
            page: page,
            pageSize: pageSize,
            cancellationToken: cancellationToken);

        return Ok(new PaginatedResult<EventResponseDto>
        {
            TotalCount = result.TotalCount,
            Page = result.Page,
            Count = result.Count,
            Items = result.Items.Select(ToResponseDto).ToList()
        });
    }

    /// <summary>
    /// Получить топ-10 мероприятий по проценту проданных мест.
    /// </summary>
    [HttpGet("top")]
    [ProducesResponseType(typeof(IReadOnlyList<EventResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EventResponseDto>>> GetTop(
        CancellationToken cancellationToken = default)
    {
        var top = await _eventService.GetTopSellingAsync(cancellationToken);

        return Ok(top.Select(ToResponseDto).ToList());
    }

    /// <summary>
    /// Получить мероприятие по id.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponseDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var evt = await _eventService.GetByIdAsync(id, cancellationToken);
        if (evt is null)
            throw new NotFoundException("Event not found.");

        return Ok(ToResponseDto(evt));
    }

    /// <summary>
    /// Создать мероприятие.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventResponseDto>> Create(
        [FromBody] EventCreateDto dto,
        CancellationToken cancellationToken = default)
    {
        var created = await _eventService.CreateAsync(dto, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            ToResponseDto(created));
    }

    /// <summary>
    /// Полностью обновить мероприятие по id.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] EventUpdateDto dto,
        CancellationToken cancellationToken = default)
    {
        var updated = await _eventService.UpdateAsync(id, dto, cancellationToken);
        if (!updated)
            throw new NotFoundException("Event not found.");

        return NoContent();
    }

    /// <summary>
    /// Удалить мероприятие по id.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await _eventService.DeleteAsync(id, cancellationToken);
        if (!deleted)
            throw new NotFoundException("Event not found.");

        return NoContent();
    }
}
