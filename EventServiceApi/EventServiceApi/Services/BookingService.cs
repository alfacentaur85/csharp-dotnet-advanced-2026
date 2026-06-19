using EventServiceApi.Enums;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using System.Collections.Concurrent;

namespace EventServiceApi.Services;

public sealed class BookingService : IBookingService
{
    private readonly ConcurrentDictionary<Guid, Booking> _storage = new();
    private readonly IEventService _eventService;

    private readonly object _bookingLock = new();

    public BookingService(IEventService eventService)
    {
        _eventService = eventService;
    }
    public Task<bool> TryRejectPendingAsync(Guid bookingId, string? reason = null)
    {
        lock (_bookingLock)
        {
            if (!_storage.TryGetValue(bookingId, out var current))
                return Task.FromResult(false);

            if (current.Status != BookingStatus.Pending)
                return Task.FromResult(false);

            // событие могло быть удалено — тогда место вернуть некуда
            var evt = _eventService.GetById(current.EventId);
            if (evt is not null)
            {
                evt.ReleaseSeats(1);
            }

            var rejected = new Booking
            {
                Id = current.Id,
                EventId = current.EventId,
                Status = BookingStatus.Rejected,
                CreatedAt = current.CreatedAt,
                ProcessedAt = DateTime.UtcNow
            };

            _storage[bookingId] = rejected;
            return Task.FromResult(true);
        }
    }

    public Task<Booking> CreateBookingAsync(Guid eventId)
    {
        lock (_bookingLock)
        {
            var evt = _eventService.GetById(eventId) ?? throw new NotFoundException("Событие не найдено.");

            if (!evt.TryReserveSeats(1))
                throw new NoAvailableSeatsException();

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