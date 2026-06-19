using EventServiceApi.Enums;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using EventServiceApi.Services;
using Moq;

namespace EventServiceApi.Tests;

public class BookingServiceTests_NewLogic
{
    private static Event CreateTestEvent(Guid id, int totalSeats)
    {
        return new Event
        {
            Id = id,
            Title = "Test",
            StartAt = DateTime.UtcNow,
            EndAt = DateTime.UtcNow.AddHours(1),
            TotalSeats = totalSeats,
            AvailableSeats = totalSeats
        };
    }

    private static (BookingService bookingService, Event? evt, Mock<IEventService> eventServiceMock)
        CreateSut(Event? evt)
    {
        var eventServiceMock = new Mock<IEventService>();

        // Возвращаем evt только если id совпадает, иначе null
        eventServiceMock
            .Setup(s => s.GetById(It.IsAny<Guid>()))
            .Returns<Guid>(id => evt is not null && evt.Id == id ? evt : null);

        var bookingService = new BookingService(eventServiceMock.Object);
        return (bookingService, evt, eventServiceMock);
    }

    // -------------------------
    // Успешные сценарии (места)
    // -------------------------

    [Fact]
    public async Task CreateBooking_DecreasesAvailableSeatsBy1()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);
        var (sut, _, _) = CreateSut(evt);

        var booking = await sut.CreateBookingAsync(eventId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(2, evt.AvailableSeats);
    }

    [Fact]
    public async Task CreateMultipleBookings_UntilLimit_AllSuccessful_UniqueIds()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 5);
        var (sut, _, _) = CreateSut(evt);

        var bookings = new List<Booking>();
        for (int i = 0; i < 5; i++)
            bookings.Add(await sut.CreateBookingAsync(eventId));

        Assert.Equal(0, evt.AvailableSeats);
        Assert.Equal(5, bookings.Count);
        Assert.Equal(5, bookings.Select(b => b.Id).Distinct().Count());
    }

    [Fact]
    public async Task CreateBooking_AfterSeatsExhausted_ThrowsNoAvailableSeatsException()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);
        var (sut, _, _) = CreateSut(evt);

        await sut.CreateBookingAsync(eventId);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => sut.CreateBookingAsync(eventId));
        Assert.Equal(0, evt.AvailableSeats);
    }

    // -------------------------
    // Неуспешные сценарии
    // -------------------------

    [Fact]
    public async Task CreateBooking_ForNonExistingEvent_ThrowsNotFoundException()
    {
        var (sut, _, _) = CreateSut(evt: null);

        await Assert.ThrowsAsync<NotFoundException>(() => sut.CreateBookingAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateBooking_WhenNoSeats_ThrowsNoAvailableSeatsException()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);
        evt.AvailableSeats = 0; // принудительно нет мест
        var (sut, _, _) = CreateSut(evt);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => sut.CreateBookingAsync(eventId));
    }

    // -------------------------
    // Тесты смены статуса брони
    // -------------------------

    [Fact]
    public async Task TryProcessPendingAsync_ChangesStatusToConfirmed_AndSetsProcessedAt()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);
        var (sut, _, _) = CreateSut(evt);

        var created = await sut.CreateBookingAsync(eventId);

        var ok = await sut.TryProcessPendingAsync(created.Id);
        var loaded = await sut.GetBookingByIdAsync(created.Id);

        Assert.True(ok);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Confirmed, loaded!.Status);
        Assert.NotNull(loaded.ProcessedAt);
        Assert.True(loaded.ProcessedAt >= loaded.CreatedAt);
    }

    [Fact]
    public async Task TryRejectPendingAsync_ChangesStatusToRejected_AndSetsProcessedAt()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);
        var (sut, _, _) = CreateSut(evt);

        var created = await sut.CreateBookingAsync(eventId);

        var ok = await sut.TryRejectPendingAsync(created.Id, "test");
        var loaded = await sut.GetBookingByIdAsync(created.Id);

        Assert.True(ok);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Rejected, loaded!.Status);
        Assert.NotNull(loaded.ProcessedAt);
        Assert.True(loaded.ProcessedAt >= loaded.CreatedAt);
    }

    [Fact]
    public async Task AfterReject_AvailableSeatsRestored()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);
        var (sut, _, _) = CreateSut(evt);

        var created = await sut.CreateBookingAsync(eventId);
        Assert.Equal(0, evt.AvailableSeats);

        var rejected = await sut.TryRejectPendingAsync(created.Id, "test");
        Assert.True(rejected);

        Assert.Equal(1, evt.AvailableSeats);
    }

    [Fact]
    public async Task AfterReject_CanCreateNewBookingOnSameSeat()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);
        var (sut, _, _) = CreateSut(evt);

        var first = await sut.CreateBookingAsync(eventId);
        Assert.Equal(0, evt.AvailableSeats);

        var rejected = await sut.TryRejectPendingAsync(first.Id, "test");
        Assert.True(rejected);
        Assert.Equal(1, evt.AvailableSeats);

        var second = await sut.CreateBookingAsync(eventId);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(0, evt.AvailableSeats);
    }

    // -------------------------
    // (Опционально) Unit-тесты доменных методов Confirm/Reject,
    // если вы их добавили в Booking
    // -------------------------

    [Fact]
    public void Booking_Confirm_SetsConfirmed_AndProcessedAt()
    {
        var b = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        b.Confirm();

        Assert.Equal(BookingStatus.Confirmed, b.Status);
        Assert.NotNull(b.ProcessedAt);
    }

    [Fact]
    public void Booking_Reject_SetsRejected_AndProcessedAt()
    {
        var b = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProcessedAt = null
        };

        b.Reject();

        Assert.Equal(BookingStatus.Rejected, b.Status);
        Assert.NotNull(b.ProcessedAt);
    }

    // -------------------------
    // Тесты конкурентности
    // -------------------------

    [Fact]
    public async Task Concurrency_OverbookingProtection_5Seats_20Requests_Only5Success()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 5);
        var (sut, _, _) = CreateSut(evt);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    var b = await sut.CreateBookingAsync(eventId);
                    return (Success: true, Booking: b, Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Success: false, Booking: (Booking?)null, Error: ex);
                }
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r.Success);
        var noSeatsCount = results.Count(r => r.Error is NoAvailableSeatsException);

        Assert.Equal(5, successCount);
        Assert.Equal(15, noSeatsCount);
        Assert.Equal(0, evt.AvailableSeats);
    }

    [Fact]
    public async Task Concurrency_UniqueIds_10Seats_10Requests_AllUnique()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 10);
        var (sut, _, _) = CreateSut(evt);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => sut.CreateBookingAsync(eventId)))
            .ToArray();

        var bookings = await Task.WhenAll(tasks);

        Assert.Equal(10, bookings.Length);
        Assert.Equal(10, bookings.Select(b => b.Id).Distinct().Count());
        Assert.Equal(0, evt.AvailableSeats);
    }
}