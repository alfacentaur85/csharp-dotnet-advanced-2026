using EventService.Infrastructure.DataAccess;
using EventService.Domain.Enums;
using EventService.Domain.Exceptions;
using EventService.Application.Interfaces;
using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Tests;

public class BookingServiceTests : TestDiFixture
{
    private static Event CreateTestEvent(Guid id, int totalSeats = 10, DateTime? startAt = null)
    {
        var start = startAt ?? new DateTime(2030, 06, 01, 10, 0, 0, DateTimeKind.Utc);

        return new()
        {
            Id = id,
            Title = "Test",
            StartAt = start,
            EndAt = start.AddHours(1),
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats
        };
    }

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

        var booking = await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);

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

        var b1 = await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);
        var b2 = await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);
        var b3 = await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);


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
            var created = await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);
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
            bookingId = (await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct)).Id;
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
            bookingId = (await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct)).Id;
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
            await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);

            await Assert.ThrowsAsync<NoAvailableSeatsException>(() =>
                bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct));
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
            bookingService.CreateBookingAsync(Guid.NewGuid(), Guid.NewGuid(), ct));
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
            bookingId = (await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct)).Id;
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
            firstId = (await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct)).Id;
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
            secondId = (await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct)).Id;
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
                    var b = await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);
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
                return await bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct);
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

    [Fact]
    public async Task CreateBooking_ForPastEvent_ThrowsPastEventBookingException()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();

        await SeedEventAsync(
            CreateTestEvent(eventId, totalSeats: 3, startAt: new DateTime(2020, 01, 01, 10, 0, 0, DateTimeKind.Utc)),
            ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        await Assert.ThrowsAsync<PastEventBookingException>(() =>
            bookingService.CreateBookingAsync(eventId, Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task CreateBooking_WhenUserHasTenActiveBookings_ThrowsActiveBookingsLimitExceededException()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 20), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        for (var i = 0; i < 10; i++)
        {
            await bookingService.CreateBookingAsync(eventId, userId, ct);
        }

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(() =>
            bookingService.CreateBookingAsync(eventId, userId, ct));
    }

    [Fact]
    public async Task CreateBooking_ActiveBookingsLimit_IsPerUser_DoesNotAffectOtherUsers()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 20), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        for (var i = 0; i < 10; i++)
        {
            await bookingService.CreateBookingAsync(eventId, userA, ct);
        }

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(() =>
            bookingService.CreateBookingAsync(eventId, userA, ct));

        // лимит userA исчерпан, но userB должен спокойно забронировать
        var bookingB = await bookingService.CreateBookingAsync(eventId, userB, ct);

        Assert.NotEqual(Guid.Empty, bookingB.Id);
        Assert.Equal(userB, bookingB.UserId);
    }

    [Fact]
    public async Task CancelBooking_ByOwner_SetsCancelled_AndReleasesSeat()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, userId, ct);

        var cancelled = await bookingService.CancelBookingAsync(booking.Id, userId, UserRole.User, ct);
        Assert.True(cancelled);

        var loaded = await LoadBookingAsync(booking.Id, ct);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Cancelled, loaded!.Status);
        Assert.NotNull(loaded.ProcessedAt);

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtFromDb.AvailableSeats);
    }

    [Fact]
    public async Task CancelBooking_ByAdmin_ForOtherUsersBooking_Succeeds()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, ownerId, ct);

        var cancelled = await bookingService.CancelBookingAsync(booking.Id, adminId, UserRole.Admin, ct);
        Assert.True(cancelled);

        var loaded = await LoadBookingAsync(booking.Id, ct);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Cancelled, loaded!.Status);

        var evtFromDb = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtFromDb.AvailableSeats);
    }

    [Fact]
    public async Task CancelBooking_ByOtherUser_ThrowsForbiddenOperationException()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, ownerId, ct);

        await Assert.ThrowsAsync<ForbiddenOperationException>(() =>
            bookingService.CancelBookingAsync(booking.Id, otherUserId, UserRole.User, ct));

        var loaded = await LoadBookingAsync(booking.Id, ct);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Pending, loaded!.Status);
    }

    [Fact]
    public async Task CancelBooking_AlreadyCancelledOrRejected_ThrowsValidationException()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, userId, ct);

        var cancelled = await bookingService.CancelBookingAsync(booking.Id, userId, UserRole.User, ct);
        Assert.True(cancelled);

        await Assert.ThrowsAsync<ValidationException>(() =>
            bookingService.CancelBookingAsync(booking.Id, userId, UserRole.User, ct));
    }

    [Fact]
    public async Task CancelBooking_UnknownId_ReturnsFalse()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var cancelled = await bookingService.CancelBookingAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.User, ct);

        Assert.False(cancelled);
    }

    [Fact]
    public async Task DeleteBooking_ForActiveBooking_RemovesIt_AndReleasesSeat()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, userId, ct);

        var evtAfterCreate = await LoadEventAsync(eventId, ct);
        Assert.Equal(0, evtAfterCreate.AvailableSeats);

        var deleted = await bookingService.DeleteBookingAsync(booking.Id, ct);
        Assert.True(deleted);

        var loaded = await LoadBookingAsync(booking.Id, ct);
        Assert.Null(loaded);

        var evtAfterDelete = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtAfterDelete.AvailableSeats);
    }

    [Fact]
    public async Task DeleteBooking_ForCancelledBooking_RemovesIt_WithoutReleasingSeatAgain()
    {
        var ct = CancellationToken.None;
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await SeedEventAsync(CreateTestEvent(eventId, totalSeats: 1), ct);

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var booking = await bookingService.CreateBookingAsync(eventId, userId, ct);
        await bookingService.CancelBookingAsync(booking.Id, userId, UserRole.User, ct);

        var evtAfterCancel = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtAfterCancel.AvailableSeats);

        var deleted = await bookingService.DeleteBookingAsync(booking.Id, ct);
        Assert.True(deleted);

        var loaded = await LoadBookingAsync(booking.Id, ct);
        Assert.Null(loaded);

        var evtAfterDelete = await LoadEventAsync(eventId, ct);
        Assert.Equal(1, evtAfterDelete.AvailableSeats);
    }

    [Fact]
    public async Task DeleteBooking_UnknownId_ReturnsFalse()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();

        var deleted = await bookingService.DeleteBookingAsync(Guid.NewGuid(), ct);

        Assert.False(deleted);
    }
}