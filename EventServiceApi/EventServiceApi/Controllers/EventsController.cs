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
        EndAt = evt.EndAt
    };

    /// <summary>
    /// Получить список всех мероприятий.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponseDto>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<EventResponseDto>> GetAll()
    {
        var events = _eventService.GetAll();
        return Ok(events.Select(ToResponseDto));
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
        // При [ApiController] ручная валидация обычно не нужна, но можно оставить.
        if (!TryValidateModel(dto))
            return ValidationProblem(ModelState);

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
        if (!TryValidateModel(dto))
            return ValidationProblem(ModelState);

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
}