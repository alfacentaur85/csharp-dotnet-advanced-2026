using EventServiceApi.Dtos;
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

    /// <summary>
    /// Получить список всех мероприятий.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Event>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<Event>> GetAll()
    {
        var events = _eventService.GetAll();
        return Ok(events);
    }

    /// <summary>
    /// Получить мероприятие по id.
    /// </summary>
    /// <param name="id">Идентификатор мероприятия.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Event), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<Event> GetById(Guid id)
    {
        var evt = _eventService.GetById(id);
        if (evt is null)
            return NotFound();

        return Ok(evt);
    }

    /// <summary>
    /// Создать мероприятие.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Event), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<Event> Create([FromBody] EventCreateUpdateDto dto)
    {
        // [ApiController] автоматически вернет 400 при невалидной модели,

        var evt = new Event
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt
        };

        var created = _eventService.Create(evt);

        return CreatedAtAction(
            nameof(GetById),
            new { id = created.Id },
            created);
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
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var evt = new Event
        {
            Id = id,
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt
        };

        var updated = _eventService.Update(id, evt);
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