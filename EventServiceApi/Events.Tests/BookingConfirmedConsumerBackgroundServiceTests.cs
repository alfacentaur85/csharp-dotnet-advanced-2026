using System.Text.Json;
using Contracts;
using Events.Application.Caching;
using Events.Application.Interfaces;
using Events.Domain.Entities;
using Events.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Events.Tests;

/// <summary>
/// Проверяет тот же сценарий инвалидации кеша (<see cref="EventServiceCacheTests"/> покрывает
/// update/delete со стороны Events.Api), но со стороны Kafka-обработчика
/// (<see cref="BookingConfirmedConsumerBackgroundService.HandleMessageAsync"/>), который списывает
/// места и удаляет ключ event:{id} самостоятельно, без обращения к EventService.
/// </summary>
public class BookingConfirmedConsumerBackgroundServiceTests
{
    private static (BookingConfirmedConsumerBackgroundService Sut, Mock<IEventRepository> Repository, Mock<ICacheService> Cache)
        CreateSut(Event? existing)
    {
        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var processedBookingEventStore = new Mock<IProcessedBookingEventStore>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var cache = new Mock<ICacheService>();

        var services = new ServiceCollection();
        services.AddSingleton(repository.Object);
        services.AddSingleton(processedBookingEventStore.Object);
        services.AddSingleton(unitOfWork.Object);
        services.AddSingleton(cache.Object);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        var sut = new BookingConfirmedConsumerBackgroundService(
            Options.Create(new KafkaConsumerOptions()),
            scopeFactory,
            NullLogger<BookingConfirmedConsumerBackgroundService>.Instance);

        return (sut, repository, cache);
    }

    private static Event MakeEvent(Guid id, int availableSeats) => new()
    {
        Id = id,
        Title = "Test",
        StartAt = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
        EndAt = new DateTime(2026, 06, 01, 11, 0, 0, DateTimeKind.Utc),
        TotalSeats = 10,
        AvailableSeats = availableSeats
    };

    private static string ToMessage(Guid eventId, int seatsCount) => JsonSerializer.Serialize(new BookingConfirmedEvent(
        BookingId: Guid.NewGuid(),
        EventId: eventId,
        UserId: Guid.NewGuid(),
        SeatsCount: seatsCount,
        ConfirmedAt: DateTime.UtcNow));

    [Fact]
    public async Task HandleMessageAsync_SeatsReserved_InvalidatesEventCache()
    {
        var eventId = Guid.NewGuid();
        var (sut, _, cache) = CreateSut(MakeEvent(eventId, availableSeats: 5));

        await sut.HandleMessageAsync(ToMessage(eventId, seatsCount: 1), CancellationToken.None);

        cache.Verify(c => c.RemoveAsync(EventCacheKeys.EventById(eventId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleMessageAsync_NotEnoughSeats_DoesNotTouchCache()
    {
        var eventId = Guid.NewGuid();
        var (sut, _, cache) = CreateSut(MakeEvent(eventId, availableSeats: 0));

        await sut.HandleMessageAsync(ToMessage(eventId, seatsCount: 1), CancellationToken.None);

        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleMessageAsync_UnknownEventId_DoesNotTouchCache()
    {
        var (sut, _, cache) = CreateSut(existing: null);

        await sut.HandleMessageAsync(ToMessage(Guid.NewGuid(), seatsCount: 1), CancellationToken.None);

        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
