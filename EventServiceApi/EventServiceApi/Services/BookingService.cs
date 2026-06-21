using EventServiceApi.DataAccess;
using EventServiceApi.Enums;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EventServiceApi.Services;

public sealed class BookingService : IBookingService
{
    private readonly AppDbContext _context;

    private static readonly SemaphoreSlim _bookingSemaphore = new(1, 1);

    public BookingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            // ВАЖНО: без AsNoTracking, чтобы Event отслеживался и изменение AvailableSeats сохранилось.
            var evt = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

            if (evt is null)
                throw new NotFoundException("Event not found.");

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

            _context.Bookings.Add(booking);

            // Один SaveChangesAsync сохранит и бронь, и изменение AvailableSeats у evt (оба отслеживаются).
            await _context.SaveChangesAsync(cancellationToken);

            return booking;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public Task<Booking?> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        => _context.Bookings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

    public async Task<IReadOnlyCollection<Booking>> GetPendingBookingsAsync(CancellationToken cancellationToken = default)
    {
        // Снимок pending-броней на текущий момент
        var pending = await _context.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return pending;
    }

    public async Task<bool> TryUpdateBookingAsync(Booking booking, CancellationToken cancellationToken = default)
    {
        var existing = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == booking.Id, cancellationToken);

        if (existing is null)
            return false;

        // CreatedAt не меняем
        existing.Status = booking.Status;
        existing.ProcessedAt = booking.ProcessedAt;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> TryProcessPendingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

            if (booking is null)
                return false;

            if (booking.Status != BookingStatus.Pending)
                return false;

            booking.Confirm();

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }

    public async Task<bool> TryRejectPendingAsync(
        Guid bookingId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        await _bookingSemaphore.WaitAsync(cancellationToken);
        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

            if (booking is null)
                return false;

            if (booking.Status != BookingStatus.Pending)
                return false;

            // событие могло быть удалено — тогда место вернуть некуда
            var evt = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == booking.EventId, cancellationToken);

            if (evt is not null)
            {
                evt.ReleaseSeats(1);
            }

            booking.Reject();

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        finally
        {
            _bookingSemaphore.Release();
        }
    }
}