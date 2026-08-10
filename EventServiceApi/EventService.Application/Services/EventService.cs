using System.ComponentModel.DataAnnotations;
using EventService.Application.Dto;
using EventService.Application.Interfaces;
using EventService.Domain.Entities;

namespace EventService.Application.Services;

/// <summary>
/// Реализация сервиса мероприятий (бизнес-логика; доступ к данным — через IEventRepository).
/// </summary>
public sealed class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public EventService(IEventRepository eventRepository, IUnitOfWork unitOfWork)
    {
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PaginatedResult<Event>> GetAllAsync(
        string? title = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ArgumentException("Дата начала события не может быть больше даты окончания события");

        if (page < 1) throw new ArgumentException("page должен быть >= 1");
        if (pageSize < 1) throw new ArgumentException("pageSize должен быть >= 1");

        var (items, totalCount) = await _eventRepository.GetPagedAsync(title, from, to, page, pageSize, cancellationToken);

        return new PaginatedResult<Event>
        {
            TotalCount = totalCount,
            Page = page,
            Count = items.Count,
            Items = items
        };
    }

    public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _eventRepository.GetByIdAsync(id, cancellationToken);

    public async Task<Event> CreateAsync(EventCreateDto dto, CancellationToken cancellationToken = default)
    {
        var evt = Event.Create(
            title: dto.Title,
            description: dto.Description,
            startAt: dto.StartAt,
            endAt: dto.EndAt,
            totalSeats: dto.TotalSeats);

        _eventRepository.Add(evt);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return evt;
    }

    public async Task<bool> UpdateAsync(Guid id, EventUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var existing = await _eventRepository.GetByIdTrackedAsync(id, cancellationToken);

        if (existing is null)
            return false;

        ValidateDates(dto.StartAt, dto.EndAt);

        // Сколько мест уже занято по текущему состоянию
        var occupied = existing.TotalSeats - existing.AvailableSeats;
        if (occupied < 0) occupied = 0;

        if (dto.TotalSeats.HasValue)
        {
            var newTotalSeats = dto.TotalSeats.Value;

            if (newTotalSeats <= 0)
                throw new ValidationException("TotalSeats должен быть больше нуля.");

            if (newTotalSeats < occupied)
                throw new ValidationException("Нельзя уменьшить TotalSeats ниже количества уже занятых мест.");

            existing.TotalSeats = newTotalSeats;
            existing.AvailableSeats = newTotalSeats - occupied;
        }

        existing.Title = dto.Title;
        existing.Description = dto.Description;
        existing.StartAt = dto.StartAt;
        existing.EndAt = dto.EndAt;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await _eventRepository.GetByIdTrackedAsync(id, cancellationToken);

        if (existing is null)
            return false;

        _eventRepository.Remove(existing);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateDates(DateTime start, DateTime end)
    {
        if (end <= start)
            throw new ValidationException("Дата окончания должна быть позже даты начала.");
    }
}
