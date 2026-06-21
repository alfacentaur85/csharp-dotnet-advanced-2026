using EventServiceApi.DataAccess;
using EventServiceApi.Enums;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventServiceApi.Tests;

public class BookingServiceTests : TestDiFixture
{
    private static Event CreateTestEvent(Guid id, int totalSeats = 10)
        => new()
        {
            Id = id,
            Title = "Test",
            StartAt = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
            EndAt = new DateTime(2026, 06, 01, 11, 0, 0, DateTimeKind.Utc),
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats
        };

    private async Task SeedEventAsync(Event evt, CancellationToken ct)
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Events.Add(evt);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Event> LoadEventAsync(Guid eventId, CancellationToken ct)
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Events.AsNoTracking().FirstAsync(e => e.Id == eventId, ct);
    }

    private async Task<Booking?> LoadBookingAsync(Guid bookingId, CancellationToken ct)
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == bookingId, ct);
    }

    private async Task<int> CountBookingsForEventAsync(Guid eventId, CancellationToken ct)
    {
        using var scope = ServiceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.CountAsync(b => b.EventId == eventId, ct);
    }

    [Fact]
    public async Task CreateBooking_ForExistingEvent_ReturnsPendingBooking()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 3), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, ct);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.NotEqual(default, booking.CreatedAt);
        Assert.Null(booking.ProcessedAt);

        var fromDb = await LoadBookingAsync(booking.Id, ct);
        Assert.NotNull(fromDb);

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(2, evtFromDb.AvailableSeats);
    }

    [Fact]
    public async Task CreateMultipleBookings_ForSameEvent_AllHaveUniqueIds()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 3), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var b1 = await bookingService.CreateBookingAsync(eventId, ct);
        var b2 = await bookingService.CreateBookingAsync(eventId, ct);
        var b3 = await bookingService.CreateBookingAsync(eventId, ct);

        Assert.NotEqual(b1.Id, b2.Id);
        Assert.NotEqual(b1.Id, b3.Id);
        Assert.NotEqual(b2.Id, b3.Id);

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtFromDb.AvailableSeats);

        var count = await CountBookingsForEventAsync(eventId, ct);
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task GetBookingById_ReturnsCorrectBooking()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 3), ct);

        Guid bookingId;
        DateTime createdAt;

        // создаём бронь в одном scope
        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var created = await bookingService.CreateBookingAsync(eventId, ct);
            bookingId = created.Id;
            createdAt = created.CreatedAt;
        }

        // читаем в другом scope
        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var loaded = await bookingService.GetBookingByIdAsync(bookingId, ct);

            Assert.NotNull(loaded);
            Assert.Equal(bookingId, loaded!.Id);
            Assert.Equal(eventId, loaded.EventId);
            Assert.Equal(BookingStatus.Pending, loaded.Status);
            Assert.Equal(createdAt, loaded.CreatedAt);
        }
    }

    [Fact]
    public async Task GetBookingById_ForUnknownId_ReturnsNull()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var loaded = await bookingService.GetBookingByIdAsync(Guid.NewGuid(), ct);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForPendingBooking_SetsConfirmedAndProcessedAt()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 3), ct);

        Guid bookingId;

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            bookingId = (await bookingService.CreateBookingAsync(eventId, ct)).Id;
        }

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            var processed = await bookingService.TryProcessPendingAsync(bookingId, ct);
            Assert.True(processed);

            var loaded = await bookingService.GetBookingByIdAsync(bookingId, ct);
            Assert.NotNull(loaded);
            Assert.Equal(BookingStatus.Confirmed, loaded!.Status);
            Assert.NotNull(loaded.ProcessedAt);
            Assert.True(loaded.ProcessedAt!.Value >= loaded.CreatedAt);
        }
    }

    [Fact]
    public async Task TryProcessPendingAsync_WhenAlreadyProcessed_ReturnsFalse_AndDoesNotOverwriteProcessedAt()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 3), ct);

        Guid bookingId;

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            bookingId = (await bookingService.CreateBookingAsync(eventId, ct)).Id;
        }

        DateTime? processedAt1;

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            var first = await bookingService.TryProcessPendingAsync(bookingId, ct);
            Assert.True(first);

            var afterFirst = await bookingService.GetBookingByIdAsync(bookingId, ct);
            Assert.NotNull(afterFirst);
            processedAt1 = afterFirst!.ProcessedAt;
            Assert.NotNull(processedAt1);
        }

        await Task.Delay(10, ct);

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            var second = await bookingService.TryProcessPendingAsync(bookingId, ct);
            Assert.False(second);

            var afterSecond = await bookingService.GetBookingByIdAsync(bookingId, ct);
            Assert.NotNull(afterSecond);
            Assert.Equal(BookingStatus.Confirmed, afterSecond!.Status);
            Assert.Equal(processedAt1, afterSecond.ProcessedAt);
        }
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForUnknownBooking_ReturnsFalse()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var processed = await bookingService.TryProcessPendingAsync(Guid.NewGuid(), ct);

        Assert.False(processed);
    }

    [Fact]
    public async Task CreateBooking_AfterSeatsExhausted_ThrowsNoAvailableSeatsException()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            await bookingService.CreateBookingAsync(eventId, ct);

            await Assert.ThrowsAsync<NoAvailableSeatsException>(() =>
                bookingService.CreateBookingAsync(eventId, ct));
        }

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtFromDb.AvailableSeats);

        var count = await CountBookingsForEventAsync(eventId, ct);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateBooking_ForNonExistingEvent_ThrowsNotFoundException()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            bookingService.CreateBookingAsync(Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task TryRejectPendingAsync_Rejected_SetsProcessedAt_AndRestoresSeat()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        Guid bookingId;

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            bookingId = (await bookingService.CreateBookingAsync(eventId, ct)).Id;
        }

        var evtAfterCreate = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtAfterCreate.AvailableSeats);

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

            var ok = await bookingService.TryRejectPendingAsync(bookingId, "test", ct);
            Assert.True(ok);

            var loaded = await bookingService.GetBookingByIdAsync(bookingId, ct);
            Assert.NotNull(loaded);
            Assert.Equal(BookingStatus.Rejected, loaded!.Status);
            Assert.NotNull(loaded.ProcessedAt);
        }

        var evtAfterReject = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtAfterReject.AvailableSeats);
    }

    [Fact]
    public async Task AfterReject_CanCreateNewBookingOnSameSeat()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        Guid firstId;

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            firstId = (await bookingService.CreateBookingAsync(eventId, ct)).Id;
        }

        var evtAfterFirst = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtAfterFirst.AvailableSeats);

        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            var rejected = await bookingService.TryRejectPendingAsync(firstId, "test", ct);
            Assert.True(rejected);
        }

        var evtAfterReject = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtAfterReject.AvailableSeats);

        Guid secondId;
        using (var scope = ServiceProvider.CreateScope())
        {
            var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
            secondId = (await bookingService.CreateBookingAsync(eventId, ct)).Id;
        }

        Assert.NotEqual(firstId, secondId);

        var evtAfterSecond = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtAfterSecond.AvailableSeats);
    }

    [Fact]
    public async Task Concurrency_OverbookingProtection_5Seats_20Requests_Only5Success()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        const int seats = 5;
        const int concurrentRequests = 20;

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: seats), ct);

        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = ServiceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

                try
                {
                    var b = await bookingService.CreateBookingAsync(eventId, ct);
                    return (Success: true, Booking: b, Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Success: false, Booking: (Booking?)null, Error: ex);
                }
            }, ct))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.Success);
        var noSeatsCount = results.Count(r => r.Error is NoAvailableSeatsException);

        Assert.Equal(seats, successCount);
        Assert.Equal(concurrentRequests - seats, noSeatsCount);

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtFromDb.AvailableSeats);

        var bookingsCount = await CountBookingsForEventAsync(eventId, ct);
        Assert.Equal(seats, bookingsCount);
    }

    [Fact]
    public async Task Concurrency_UniqueIds_10Seats_10Requests_AllUnique()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        const int seats = 10;

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: seats), ct);

        var tasks = Enumerable.Range(0, seats)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = ServiceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                return await bookingService.CreateBookingAsync(eventId, ct);
            }, ct))
            .ToArray();

        var bookings = await Task.WhenAll(tasks);

        Assert.Equal(seats, bookings.Length);
        Assert.Equal(seats, bookings.Select(b => b.Id).Distinct().Count());

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtFromDb.AvailableSeats);

        var bookingsCount = await CountBookingsForEventAsync(eventId, ct);
        Assert.Equal(seats, bookingsCount);
    }
}