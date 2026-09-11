using Bookings.Application.Interfaces;
using Bookings.Application.Options;
using Bookings.Application.Services;
using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.Domain.Exceptions;
using Contracts;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.ComponentModel.DataAnnotations;

namespace Bookings.Tests;

public class BookingServiceTests
{
    private readonly Mock<IBookingRepository> _bookingRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IBookingConfirmedPublisher> _publisher = new();
    private readonly BookingService _sut;

    public BookingServiceTests()
    {
        var options = Options.Create(new BookingOptions { MaxActiveBookingsPerUser = 10 });
        _sut = new BookingService(_bookingRepository.Object, _unitOfWork.Object, _publisher.Object, options);
    }

    private static Booking CreatePendingBooking(Guid? eventId = null, Guid? userId = null)
        => Booking.Create(eventId ?? Guid.NewGuid(), userId ?? Guid.NewGuid());

    [Fact]
    public async Task CreateBookingAsync_UnderLimit_CreatesPendingBooking_AndSaves()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _bookingRepository.Setup(r => r.CountActiveByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var booking = await _sut.CreateBookingAsync(eventId, userId);

        booking.Should().NotBeNull();
        booking.EventId.Should().Be(eventId);
        booking.UserId.Should().Be(userId);
        booking.Status.Should().Be(BookingStatus.Pending);
        booking.ProcessedAt.Should().BeNull();

        _bookingRepository.Verify(r => r.Add(It.Is<Booking>(b => b.Id == booking.Id)), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateBookingAsync_WhenActiveBookingsLimitExceeded_ThrowsActiveBookingsLimitExceededException()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _bookingRepository.Setup(r => r.CountActiveByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var act = () => _sut.CreateBookingAsync(eventId, userId);

        await act.Should().ThrowAsync<ActiveBookingsLimitExceededException>();

        _bookingRepository.Verify(r => r.Add(It.IsAny<Booking>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateBookingAsync_ActiveBookingsLimit_IsPerUser_DoesNotAffectOtherUsers()
    {
        var eventId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        _bookingRepository.Setup(r => r.CountActiveByUserAsync(userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _bookingRepository.Setup(r => r.CountActiveByUserAsync(userB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await Assert.ThrowsAsync<ActiveBookingsLimitExceededException>(() => _sut.CreateBookingAsync(eventId, userA));

        var bookingB = await _sut.CreateBookingAsync(eventId, userB);

        bookingB.UserId.Should().Be(userB);
    }

    [Fact]
    public async Task GetBookingByIdAsync_DelegatesToRepository()
    {
        var booking = CreatePendingBooking();
        _bookingRepository.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await _sut.GetBookingByIdAsync(booking.Id);

        result.Should().Be(booking);
    }

    [Fact]
    public async Task GetBookingByIdAsync_ForUnknownId_ReturnsNull()
    {
        _bookingRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var result = await _sut.GetBookingByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForPendingBooking_ConfirmsSaves_AndPublishesAfterSave()
    {
        var booking = CreatePendingBooking();
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var callOrder = new List<string>();
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("save"))
            .ReturnsAsync(1);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<BookingConfirmedEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => callOrder.Add("publish"))
            .Returns(Task.CompletedTask);

        var result = await _sut.TryProcessPendingAsync(booking.Id);

        result.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Confirmed);
        booking.ProcessedAt.Should().NotBeNull();

        // Сохранение в БД должно произойти ДО публикации в Kafka.
        callOrder.Should().Equal("save", "publish");

        _publisher.Verify(p => p.PublishAsync(
            It.Is<BookingConfirmedEvent>(e =>
                e.BookingId == booking.Id &&
                e.EventId == booking.EventId &&
                e.UserId == booking.UserId &&
                e.SeatsCount == 1 &&
                e.ConfirmedAt == booking.ProcessedAt!.Value),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForUnknownBooking_ReturnsFalse_AndDoesNotPublish()
    {
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var result = await _sut.TryProcessPendingAsync(Guid.NewGuid());

        result.Should().BeFalse();
        _publisher.Verify(p => p.PublishAsync(It.IsAny<BookingConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryProcessPendingAsync_ForNonPendingBooking_ReturnsFalse_AndDoesNotPublish()
    {
        var booking = CreatePendingBooking();
        booking.Confirm();

        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await _sut.TryProcessPendingAsync(booking.Id);

        result.Should().BeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<BookingConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRejectPendingAsync_ForPendingBooking_SetsRejected_AndDoesNotPublish()
    {
        var booking = CreatePendingBooking();
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await _sut.TryRejectPendingAsync(booking.Id, "test");

        result.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Rejected);
        booking.ProcessedAt.Should().NotBeNull();

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<BookingConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TryRejectPendingAsync_ForUnknownBooking_ReturnsFalse()
    {
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var result = await _sut.TryRejectPendingAsync(Guid.NewGuid());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelBookingAsync_ByOwner_SetsCancelled()
    {
        var userId = Guid.NewGuid();
        var booking = CreatePendingBooking(userId: userId);

        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await _sut.CancelBookingAsync(booking.Id, userId, UserRole.User);

        result.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelBookingAsync_ByAdmin_ForOtherUsersBooking_Succeeds()
    {
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var booking = CreatePendingBooking(userId: ownerId);

        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await _sut.CancelBookingAsync(booking.Id, adminId, UserRole.Admin);

        result.Should().BeTrue();
        booking.Status.Should().Be(BookingStatus.Cancelled);
    }

    [Fact]
    public async Task CancelBookingAsync_ByOtherUser_ThrowsForbiddenOperationException()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var booking = CreatePendingBooking(userId: ownerId);

        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var act = () => _sut.CancelBookingAsync(booking.Id, otherUserId, UserRole.User);

        await act.Should().ThrowAsync<ForbiddenOperationException>();
        booking.Status.Should().Be(BookingStatus.Pending);
    }

    [Fact]
    public async Task CancelBookingAsync_AlreadyCancelled_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        var booking = CreatePendingBooking(userId: userId);
        booking.Cancel();

        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var act = () => _sut.CancelBookingAsync(booking.Id, userId, UserRole.User);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CancelBookingAsync_UnknownId_ReturnsFalse()
    {
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var result = await _sut.CancelBookingAsync(Guid.NewGuid(), Guid.NewGuid(), UserRole.User);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteBookingAsync_ForExistingBooking_RemovesIt()
    {
        var booking = CreatePendingBooking();
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        var result = await _sut.DeleteBookingAsync(booking.Id);

        result.Should().BeTrue();
        _bookingRepository.Verify(r => r.Remove(booking), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteBookingAsync_UnknownId_ReturnsFalse()
    {
        _bookingRepository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        var result = await _sut.DeleteBookingAsync(Guid.NewGuid());

        result.Should().BeFalse();
        _bookingRepository.Verify(r => r.Remove(It.IsAny<Booking>()), Times.Never);
    }
}
