using EventServiceApi.Enums;
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
            .Returns(evt);

        return mock;
    }

    [Fact]
    public async Task CreateBooking_ForExistingEvent_ReturnsPendingBooking()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = new Event { Id = eventId, Title = "Test", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) };

        var eventServiceMock = new Mock<IEventService>();
        eventServiceMock.Setup(s => s.GetById(eventId)).Returns(evt);

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
        var evt = new Event { Id = eventId, Title = "Test", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) };

        var eventServiceMock = new Mock<IEventService>();
        eventServiceMock.Setup(s => s.GetById(eventId)).Returns(evt);

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
        var evt = new Event { Id = eventId, Title = "Test", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) };

        var eventServiceMock = new Mock<IEventService>();
        eventServiceMock.Setup(s => s.GetById(eventId)).Returns(evt);

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
    public async Task GetBookingById_ReflectsStatusChange_AfterConfirmOrReject()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = new Event { Id = eventId, Title = "Test", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) };

        var eventServiceMock = new Mock<IEventService>();
        eventServiceMock.Setup(s => s.GetById(eventId)).Returns(evt);

        var bookingService = new BookingService(eventServiceMock.Object);

        var created = await bookingService.CreateBookingAsync(eventId);

        // act: имитируем Confirm (в вашем enum это Approved)
        var updated = new Booking
        {
            Id = created.Id,
            EventId = created.EventId,
            Status = BookingStatus.Approved,
            CreatedAt = created.CreatedAt,
            ProcessedAt = DateTime.UtcNow
        };

        var ok = await bookingService.TryUpdateBookingAsync(updated);
        var loaded = await bookingService.GetBookingByIdAsync(created.Id);

        // assert
        Assert.True(ok);
        Assert.NotNull(loaded);
        Assert.Equal(BookingStatus.Approved, loaded!.Status);
        Assert.NotNull(loaded.ProcessedAt);
    }

    [Fact]
    public async Task CreateBooking_ForNonExistingEvent_ThrowsKeyNotFound()
    {
        // arrange
        var eventId = Guid.NewGuid();

        var eventServiceMock = new Mock<IEventService>();
        eventServiceMock.Setup(s => s.GetById(eventId)).Returns((Event?)null);

        var bookingService = new BookingService(eventServiceMock.Object);

        // act + assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bookingService.CreateBookingAsync(eventId));
    }

    [Fact]
    public async Task CreateBooking_ForDeletedEvent_ThrowsKeyNotFound()
    {
        // По сути то же самое, что "несуществующее": GetById возвращает null
        // arrange
        var eventId = Guid.NewGuid();

        var eventServiceMock = new Mock<IEventService>();
        eventServiceMock.Setup(s => s.GetById(eventId)).Returns((Event?)null);

        var bookingService = new BookingService(eventServiceMock.Object);

        // act + assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => bookingService.CreateBookingAsync(eventId));
    }

    [Fact]
    public async Task GetBookingById_ForUnknownId_ReturnsNull()
    {
        // arrange
        var eventId = Guid.NewGuid();
        var evt = new Event { Id = eventId, Title = "Test", StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddHours(1) };

        var eventServiceMock = new Mock<IEventService>();
        // даже если событие есть, мы ищем несуществующую бронь
        eventServiceMock.Setup(s => s.GetById(It.IsAny<Guid>())).Returns(evt);

        var bookingService = new BookingService(eventServiceMock.Object);

        // act
        var loaded = await bookingService.GetBookingByIdAsync(Guid.NewGuid());

        // assert
        Assert.Null(loaded);
    }
}