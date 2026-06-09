using EventServiceApi.Dto;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventServiceApi.Controllers;

/// <summary>
/// REST API для управления мероприятиями.
/// </summary>
[ApiController]
[Route("events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    private readonly IBookingService _bookingService;

    /// <summary>
    /// Конструктор контроллера мероприятий.
    /// </summary>
    public EventsController(IEventService eventService, IBookingService bookingService)
    {
        _eventService = eventService;
        _bookingService = bookingService;
    }

    private static EventResponseDto ToResponseDto(Event evt) => new()
    {
        Id = evt.Id,
        Title = evt.Title,
        Description = evt.Description,
        StartAt = evt.StartAt,
        EndAt = evt.EndAt
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
    public ActionResult<PaginatedResult<EventResponseDto>> GetAll(
       [FromQuery] string? title,
       [FromQuery] DateTime? from,
       [FromQuery] DateTime? to,
       [FromQuery] int page = 1,
       [FromQuery] int pageSize = 10)
    {
        if (page < 1) return BadRequest("page должен быть >= 1");
        if (pageSize < 1) return BadRequest("pageSize должен быть >= 1");

        var result = _eventService.GetAll(title, from, to, page, pageSize);

        return Ok(new PaginatedResult<EventResponseDto>
        {
            TotalCount = result.TotalCount,
            Page = result.Page,
            Count = result.Count,
            Items = result.Items.Select(ToResponseDto).ToList()
        });
    }
    /// <summary>
    /// Получить мероприятие по id.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<EventResponseDto> GetById(Guid id)
    {
        var evt = _eventService.GetById(id);
        if (evt is null)
            return NotFound();

        return Ok(ToResponseDto(evt));
    }

    /// <summary>
    /// Создать мероприятие.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<EventResponseDto> Create([FromBody] EventCreateUpdateDto dto)
    {
        var created = _eventService.Create(dto);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            ToResponseDto(created));
    }

    /// <summary>
    /// Полностью обновить мероприятие по id.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Update(Guid id, [FromBody] EventCreateUpdateDto dto)
    {
        var updated = _eventService.Update(id, dto);
        if (!updated)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Удалить мероприятие по id.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Delete(Guid id)
    {
        var deleted = _eventService.Delete(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Создать бронь на событие.
    /// </summary>
    [HttpPost("{id:guid}/book")]
    [ProducesResponseType(typeof(BookingResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponseDto>> Book(Guid id)
    {
        // если событие не найдено — 404
        var evt = _eventService.GetById(id);
        if (evt is null)
            return NotFound();

        var booking = await _bookingService.CreateBookingAsync(id);

        var response = new BookingResponseDto
        {
            Id = booking.Id,
            EventId = booking.EventId,
            Status = booking.Status
        };

        // Location: /bookings/{bookingId}
        Response.Headers.Location = $"/bookings/{booking.Id}";

        return Accepted(response); // 202
    }
}