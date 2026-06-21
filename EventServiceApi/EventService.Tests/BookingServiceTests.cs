using EventServiceApi.Enums;
using EventServiceApi.Exceptions;
using EventServiceApi.Interfaces;
using EventServiceApi.Models;
using EventServiceApi.Services;
using Moq;


namespace EventServiceApi.Tests;

public class BookingServiceTests
{
    private static Mock<IEventService> CreateEventServiceMockReturning(Event? evt)
    {
        var mock = new Mock<IEventService>();

        mock.Setup(s => s.GetById(It.IsAny<Guid>()))
            .Returns<Guid>(id => evt is not null && evt.Id == id ? evt : null);

        return mock;
    }

    private static Event CreateTestEvent(Guid id, int totalSeats = 10)
    => new()
    {
        Id = id,
        Title = "Test",
        StartAt = DateTime.UtcNow,
        EndAt = DateTime.UtcNow.AddHours(1),
        TotalSeats = totalSeats,
        AvailableSeats = totalSeats
    };

    [Fact]
    public async Task CreateBooking_ForExistingEvent_ReturnsPendingBooking()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        // act
        var booking = await bookingService.CreateBookingAsync(eventId);

        // assert
        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.NotEqual(default, booking.CreatedAt);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public async Task CreateMultipleBookings_ForSameEvent_AllHaveUniqueIds()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        // act
        var b1 = await bookingService.CreateBookingAsync(eventId);
        var b2 = await bookingService.CreateBookingAsync(eventId);
        var b3 = await bookingService.CreateBookingAsync(eventId);

        // assert
        Assert.NotEqual(b1.Id, b2.Id);
        Assert.NotEqual(b1.Id, b3.Id);
        Assert.NotEqual(b2.Id, b3.Id);
    }

    [Fact]
    public async Task GetBookingById_ReturnsCorrectBooking()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var created = await bookingService.CreateBookingAsync(eventId);

        // act
        var loaded = await bookingService.GetBookingByIdAsync(created.Id);

        // assert
        Assert.NotNull(loaded);
        Assert.Equal(created.Id, loaded!.Id);
        Assert.Equal(created.EventId, loaded.EventId);
        Assert.Equal(created.Status, loaded.Status);
        Assert.Equal(created.CreatedAt, loaded.CreatedAt);
    }

    [Fact]
    public async Task GetBookingById_ForUnknownId_ReturnsNull()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        // act
        var loaded = await bookingService.GetBookingByIdAsync(Guid.NewGuid());

        // assert
        Assert.Null(loaded);
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForPendingBooking_SetsConfirmedAndProcessedAt()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var created = await bookingService.CreateBookingAsync(eventId);

        // act
        var processed = await bookingService.TryProcessPendingAsync(created.Id);
        var loaded = await bookingService.GetBookingByIdAsync(created.Id);

        // assert
        Assert.True(processed);
        Assert.NotNull(loaded);

        Assert.Equal(BookingStatus.Confirmed, loaded!.Status);
        Assert.NotNull(loaded.ProcessedAt);
        Assert.True(loaded.ProcessedAt!.Value >= loaded.CreatedAt);
    }

    [Fact]
    public async Task TryProcessPendingAsync_WhenAlreadyProcessed_ReturnsFalse_AndDoesNotOverwriteProcessedAt()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var created = await bookingService.CreateBookingAsync(eventId);

        // first processing
        var first = await bookingService.TryProcessPendingAsync(created.Id);
        var afterFirst = await bookingService.GetBookingByIdAsync(created.Id);
        Assert.True(first);
        Assert.NotNull(afterFirst);
        var processedAt1 = afterFirst!.ProcessedAt;

        // небольшая пауза, чтобы было заметно, если ProcessedAt перезапишется
        await Task.Delay(10);

        // act: second processing attempt
        var second = await bookingService.TryProcessPendingAsync(created.Id);
        var afterSecond = await bookingService.GetBookingByIdAsync(created.Id);

        // assert
        Assert.False(second);
        Assert.NotNull(afterSecond);
        Assert.Equal(BookingStatus.Confirmed, afterSecond!.Status);
        Assert.Equal(processedAt1, afterSecond.ProcessedAt); // не перезаписали
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForUnknownBooking_ReturnsFalse()
    {
        // arrange
        var evt = CreateTestEvent(Guid.NewGuid(), totalSeats: 3);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        // act
        var processed = await bookingService.TryProcessPendingAsync(Guid.NewGuid());

        // assert
        Assert.False(processed);
    }

    [Fact]
    public async Task CreateBooking_AfterSeatsExhausted_ThrowsNoAvailableSeatsException()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        await bookingService.CreateBookingAsync(eventId);

        await Assert.ThrowsAsync<NoAvailableSeatsException>(() =>
            bookingService.CreateBookingAsync(eventId));

        Assert.Equal(0, evt.AvailableSeats);
    }

    [Fact]
    public async Task CreateBooking_ForNonExistingEvent_ThrowsNotFoundException()
    {
        var eventId = Guid.NewGuid();

        var eventServiceMock = CreateEventServiceMockReturning(null);
        var bookingService = new BookingService(eventServiceMock.Object);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            bookingService.CreateBookingAsync(eventId));
    }

    [Fact]
    public async Task TryRejectPendingAsync_Rejected_SetsProcessedAt_AndRestoresSeat()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var created = await bookingService.CreateBookingAsync(eventId);
        Assert.Equal(0, evt.AvailableSeats);

        var ok = await bookingService.TryRejectPendingAsync(created.Id, "test");
        var loaded = await bookingService.GetBookingByIdAsync(created.Id);

        Assert.True(ok);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Rejected, loaded!.Status);
        Assert.NotNull(loaded.ProcessedAt);

        Assert.Equal(1, evt.AvailableSeats);
    }

    [Fact]
    public async Task AfterReject_CanCreateNewBookingOnSameSeat()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 1);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var first = await bookingService.CreateBookingAsync(eventId);
        Assert.Equal(0, evt.AvailableSeats);

        var rejected = await bookingService.TryRejectPendingAsync(first.Id, "test");
        Assert.True(rejected);
        Assert.Equal(1, evt.AvailableSeats);

        var second = await bookingService.CreateBookingAsync(eventId);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(0, evt.AvailableSeats);
    }

    [Fact]
    public async Task Concurrency_OverbookingProtection_5Seats_20Requests_Only5Success()
    {
        var eventId = Guid.NewGuid();
        var evt = CreateTestEvent(eventId, totalSeats: 5);

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                try
                {
                    var b = await bookingService.CreateBookingAsync(eventId);
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

        var eventServiceMock = CreateEventServiceMockReturning(evt);
        var bookingService = new BookingService(eventServiceMock.Object);

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => bookingService.CreateBookingAsync(eventId)))
            .ToArray();

        var bookings = await Task.WhenAll(tasks);

        Assert.Equal(10, bookings.Length);
        Assert.Equal(10, bookings.Select(b => b.Id).Distinct().Count());
        Assert.Equal(0, evt.AvailableSeats);
    }

}