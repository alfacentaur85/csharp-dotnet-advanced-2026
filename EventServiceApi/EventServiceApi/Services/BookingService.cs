using System.Collections.Concurrent;
using EventServiceApi.Enums;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;

namespace EventServiceApi.Services;

public sealed class BookingService : IBookingService
{
    private readonly ConcurrentDictionary<Guid, Booking> _storage = new();
    private readonly IEventService _eventService;

    public BookingService(IEventService eventService)
    {
        _eventService = eventService;
    }

    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        // Проверка существования события (нужна для 404/ошибки и для тестов)
        if (_eventService.GetById(eventId) is null)
            throw new KeyNotFoundException("Событие не найдено.");

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        while (!_storage.TryAdd(booking.Id, booking))
            booking.Id = Guid.NewGuid();

        return Task.FromResult(booking);
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId)
    {
        _storage.TryGetValue(bookingId, out var booking);
        return Task.FromResult(booking);
    }

    public Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync()
    {
        // Снимок pending-броней на текущий момент
        IReadOnlyCollection<Booking> pending = _storage.Values
            .Where(b => b.Status == BookingStatus.Pending)
            .ToList();

        return Task.FromResult(pending);
    }

    public Task<bool> TryUpdateBookingAsync(Booking booking)
    {
        if (!_storage.TryGetValue(booking.Id, out var existing))
            return Task.FromResult(false);

        // Сохраняем CreatedAt, если вдруг пришёл "пустой" (защита от некорректного апдейта)
        if (booking.CreatedAt == default)
            booking.CreatedAt = existing.CreatedAt;

        _storage[booking.Id] = booking;
        return Task.FromResult(true);
    }

    /// <summary>
    /// Атомарная обработка: если бронь всё ещё Pending — переводим в Approved и ставим ProcessedAt.
    /// Чтобы одну бронь не обработать дважды.
    /// </summary>
    public Task<bool> TryProcessPendingAsync(Guid bookingId)
    {
        while (true)
        {
            if (!_storage.TryGetValue(bookingId, out var current))
                return Task.FromResult(false);

            if (current.Status != BookingStatus.Pending)
                return Task.FromResult(false);

            var updated = new Booking
            {
                Id = current.Id,
                EventId = current.EventId,
                Status = BookingStatus.Confirmed,
                CreatedAt = current.CreatedAt,
                ProcessedAt = DateTime.UtcNow
            };

            // обновляем только если никто не поменял запись между чтением и записью
            if (_storage.TryUpdate(bookingId, updated, current))
                return Task.FromResult(true);

            // если TryUpdate не прошёл — кто-то изменил запись, повторяем
        }
    }
}