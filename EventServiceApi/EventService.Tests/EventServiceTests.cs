
using EventServiceApi.Dto;
using EventServiceApi.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace EventServiceApi.Tests;

public class EventServiceTests : TestDiFixture
{
    private static EventCreateDto CreateValidDto(
        string title = "Test",
        DateTime? start = null,
        DateTime? end = null,
        string? description = null,
        int totalSeats = 10)
    {
        var s = start ?? new DateTime(2026, 06, 01, 10, 00, 00, DateTimeKind.Utc);
        var e = end ?? s.AddHours(1);

        return new EventCreateDto
        {
            Title = title,
            StartAt = s,
            EndAt = e,
            Description = description,
            TotalSeats = totalSeats
        };
    }

    private static EventUpdateDto UpdateValidDto(
        string title = "Test",
        DateTime? start = null,
        DateTime? end = null,
        string? description = null,
        int? totalSeats = null)
    {
        var s = start ?? new DateTime(2026, 06, 01, 10, 00, 00, DateTimeKind.Utc);
        var e = end ?? s.AddHours(1);

        return new EventUpdateDto
        {
            Title = title,
            StartAt = s,
            EndAt = e,
            Description = description,
            TotalSeats = totalSeats
        };
    }

    [Fact]
    public async Task Create_ShouldCreateEvent()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var created = await service.CreateAsync(CreateValidDto(title: "My event"), ct);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("My event", created.Title);
        Assert.True(created.EndAt > created.StartAt);
        Assert.Equal(10, created.TotalSeats);
        Assert.Equal(created.TotalSeats, created.AvailableSeats);
    }

    [Fact]
    public async Task GetAll_ShouldReturnAllEvents()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        await service.CreateAsync(CreateValidDto(title: "A", start: new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc)), ct);
        await service.CreateAsync(CreateValidDto(title: "B", start: new DateTime(2026, 06, 02, 10, 0, 0, DateTimeKind.Utc)), ct);

        var result = await service.GetAllAsync(cancellationToken: ct);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetById_ShouldReturnEvent_WhenExists()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var created = await service.CreateAsync(CreateValidDto(title: "Find me"), ct);

        var found = await service.GetByIdAsync(created.Id, ct);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
    }

    [Fact]
    public async Task GetById_ShouldReturnNull_WhenNotExists()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var found = await service.GetByIdAsync(Guid.NewGuid(), ct);

        Assert.Null(found);
    }

    [Fact]
    public async Task Update_ShouldUpdateExistingEvent()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var created = await service.CreateAsync(CreateValidDto(title: "Old"), ct);

        var ok = await service.UpdateAsync(created.Id, UpdateValidDto(title: "New"), ct);

        Assert.True(ok);

        var updated = await service.GetByIdAsync(created.Id, ct);
        Assert.Equal("New", updated!.Title);
    }

    [Fact]
    public async Task Update_ShouldReturnFalse_WhenEventNotExists()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var ok = await service.UpdateAsync(Guid.NewGuid(), UpdateValidDto(title: "New"), ct);

        Assert.False(ok);
    }

    [Fact]
    public async Task Delete_ShouldDeleteExistingEvent()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var created = await service.CreateAsync(CreateValidDto(title: "To delete"), ct);

        var ok = await service.DeleteAsync(created.Id, ct);

        Assert.True(ok);

        var after = await service.GetByIdAsync(created.Id, ct);
        Assert.Null(after);
    }

    [Fact]
    public async Task Delete_ShouldReturnFalse_WhenEventNotExists()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var ok = await service.DeleteAsync(Guid.NewGuid(), ct);

        Assert.False(ok);
    }

    [Fact]
    public async Task Filter_ByTitle_ShouldBeCaseInsensitive_AndPartial()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        await service.CreateAsync(CreateValidDto(title: "DotNet Conf"), ct);
        await service.CreateAsync(CreateValidDto(title: "Java Meetup"), ct);

        var result = await service.GetAllAsync(title: "Conf", cancellationToken: ct);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("DotNet Conf", result.Items.First().Title);
    }

    [Fact]
    public async Task Filter_ByDates_ShouldWork()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        await service.CreateAsync(CreateValidDto(
            title: "E1",
            start: new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 10, 11, 0, 0, DateTimeKind.Utc)), ct);

        await service.CreateAsync(CreateValidDto(
            title: "E2",
            start: new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 01, 11, 0, 0, DateTimeKind.Utc)), ct);

        await service.CreateAsync(CreateValidDto(
            title: "E3",
            start: new DateTime(2026, 06, 20, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 25, 11, 0, 0, DateTimeKind.Utc)), ct);

        var from = new DateTime(2026, 06, 05, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 06, 15, 23, 59, 59, DateTimeKind.Utc);

        var result = await service.GetAllAsync(from: from, to: to, cancellationToken: ct);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("E1", result.Items.First().Title);
    }

    [Fact]
    public async Task Pagination_ShouldReturnCorrectPage()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        for (int i = 1; i <= 12; i++)
        {
            await service.CreateAsync(CreateValidDto(
                title: $"E{i}",
                start: new DateTime(2026, 06, i, 10, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 06, i, 11, 0, 0, DateTimeKind.Utc)), ct);
        }

        var page2 = await service.GetAllAsync(page: 2, pageSize: 5, cancellationToken: ct);

        Assert.Equal(12, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(5, page2.Count);

        Assert.Equal("E6", page2.Items.First().Title);
        Assert.Equal("E10", page2.Items.Last().Title);
    }

    [Fact]
    public async Task CombinedFiltering_ShouldWorkTogether()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        await service.CreateAsync(CreateValidDto(
            title: "Conf Moscow",
            start: new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 10, 11, 0, 0, DateTimeKind.Utc)), ct);

        await service.CreateAsync(CreateValidDto(
            title: "Conf SPB",
            start: new DateTime(2026, 07, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 07, 10, 11, 0, 0, DateTimeKind.Utc)), ct);

        await service.CreateAsync(CreateValidDto(
            title: "Meetup Moscow",
            start: new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 10, 11, 0, 0, DateTimeKind.Utc)), ct);

        var result = await service.GetAllAsync(
            title: "Conf",
            from: new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 06, 30, 23, 59, 59, DateTimeKind.Utc),
            page: 1,
            pageSize: 10,
            cancellationToken: ct);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Conf Moscow", result.Items.First().Title);
    }

    [Fact]
    public async Task Update_WithEndBeforeStart_ShouldThrowValidationException()
    {
        var ct = CancellationToken.None;

        using var scope = ServiceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEventService>();

        var id = (await service.CreateAsync(CreateValidDto(), ct)).Id;

        await Assert.ThrowsAsync<ValidationException>(async () =>
            await service.UpdateAsync(id, UpdateValidDto(
                start: new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 06, 01, 9, 0, 0, DateTimeKind.Utc)
            ), ct));
    }
}