using Events.Application.Caching;
using Events.Application.Dto;
using Events.Application.Interfaces;
using Events.Application.Options;
using Events.Application.Services;
using Events.Domain.Entities;
using Microsoft.Extensions.Options;
using Moq;

namespace Events.Tests;

public class EventServiceCacheTests
{
    private static EventService CreateSut(
        Mock<IEventRepository> repository,
        Mock<ICacheService> cache,
        Mock<IUnitOfWork>? unitOfWork = null)
    {
        return new EventService(
            repository.Object,
            (unitOfWork ?? new Mock<IUnitOfWork>()).Object,
            cache.Object,
            Options.Create(new CacheOptions { EventTtlSeconds = 300, TopEventsTtlSeconds = 600 }));
    }

    private static Event MakeEvent(Guid? id = null, int totalSeats = 10, int availableSeats = 10) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Title = "Test",
        StartAt = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
        EndAt = new DateTime(2026, 06, 01, 11, 0, 0, DateTimeKind.Utc),
        TotalSeats = totalSeats,
        AvailableSeats = availableSeats
    };

    [Fact]
    public async Task GetByIdAsync_CacheHit_DoesNotCallRepository()
    {
        var id = Guid.NewGuid();
        var cachedEvent = MakeEvent(id);

        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<Event>(EventCacheKeys.EventById(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEvent);

        var sut = CreateSut(repository, cache);

        var result = await sut.GetByIdAsync(id);

        Assert.Same(cachedEvent, result);
        repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_ReadsFromRepositoryAndPopulatesCache()
    {
        var id = Guid.NewGuid();
        var dbEvent = MakeEvent(id);

        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dbEvent);

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<Event>(EventCacheKeys.EventById(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var sut = CreateSut(repository, cache);

        var result = await sut.GetByIdAsync(id);

        Assert.Same(dbEvent, result);
        repository.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.SetAsync(
            EventCacheKeys.EventById(id),
            dbEvent,
            TimeSpan.FromSeconds(300),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_RepositoryMiss_DoesNotPopulateCache()
    {
        var id = Guid.NewGuid();

        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync((Event?)null);

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<Event>(EventCacheKeys.EventById(id), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var sut = CreateSut(repository, cache);

        var result = await sut.GetByIdAsync(id);

        Assert.Null(result);
        cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.IsAny<Event>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_OnSuccess_InvalidatesEventCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeEvent(id);

        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var unitOfWork = new Mock<IUnitOfWork>();
        var cache = new Mock<ICacheService>();

        var sut = CreateSut(repository, cache, unitOfWork);

        var dto = new EventUpdateDto
        {
            Title = "Updated",
            StartAt = existing.StartAt,
            EndAt = existing.EndAt
        };

        var ok = await sut.UpdateAsync(id, dto);

        Assert.True(ok);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(EventCacheKeys.EventById(id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenEventNotFound_DoesNotTouchCache()
    {
        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var cache = new Mock<ICacheService>();
        var sut = CreateSut(repository, cache);

        var ok = await sut.UpdateAsync(Guid.NewGuid(), new EventUpdateDto
        {
            Title = "X",
            StartAt = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
            EndAt = new DateTime(2026, 06, 01, 11, 0, 0, DateTimeKind.Utc)
        });

        Assert.False(ok);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_OnSuccess_InvalidatesEventCache()
    {
        var id = Guid.NewGuid();
        var existing = MakeEvent(id);

        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdTrackedAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var unitOfWork = new Mock<IUnitOfWork>();
        var cache = new Mock<ICacheService>();

        var sut = CreateSut(repository, cache, unitOfWork);

        var ok = await sut.DeleteAsync(id);

        Assert.True(ok);
        repository.Verify(r => r.Remove(existing), Times.Once);
        cache.Verify(c => c.RemoveAsync(EventCacheKeys.EventById(id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenEventNotFound_DoesNotTouchCache()
    {
        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetByIdTrackedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        var cache = new Mock<ICacheService>();
        var sut = CreateSut(repository, cache);

        var ok = await sut.DeleteAsync(Guid.NewGuid());

        Assert.False(ok);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTopSellingAsync_CacheHit_DoesNotCallRepository()
    {
        var cached = new List<Event> { MakeEvent() };

        var repository = new Mock<IEventRepository>(MockBehavior.Strict);
        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<List<Event>>(EventCacheKeys.Top10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cached);

        var sut = CreateSut(repository, cache);

        var result = await sut.GetTopSellingAsync();

        Assert.Same(cached, result);
        repository.Verify(r => r.GetTopSellingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetTopSellingAsync_CacheMiss_ReadsFromRepositoryAndPopulatesCache()
    {
        var items = new List<Event> { MakeEvent(totalSeats: 10, availableSeats: 2) };

        var repository = new Mock<IEventRepository>();
        repository.Setup(r => r.GetTopSellingAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(items);

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.GetAsync<List<Event>>(EventCacheKeys.Top10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((List<Event>?)null);

        var sut = CreateSut(repository, cache);

        var result = await sut.GetTopSellingAsync();

        Assert.Equal(items, result);
        cache.Verify(c => c.SetAsync(
            EventCacheKeys.Top10,
            It.IsAny<List<Event>>(),
            TimeSpan.FromSeconds(600),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
