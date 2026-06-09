using EventServiceApi.Dto;
using EventServiceApi.Services;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace EventServiceApi.Tests;

public class EventServiceTests
{
    private static EventCreateUpdateDto ValidDto(
    string title = "Test",
    DateTime? start = null,
    DateTime? end = null,
    string? description = null)
    {
        var s = start ?? new DateTime(2026, 06, 01, 10, 00, 00, DateTimeKind.Utc);
        var e = end ?? s.AddHours(1);

        return new EventCreateUpdateDto
        {
            Title = title,
            StartAt = s,   // DateTime (обязателен)
            EndAt = e,     // DateTime (обязателен)
            Description = description
        };
    }

    [Fact]
    public void Create_ShouldCreateEvent()
    {
        var service = new EventService();

        var created = service.Create(ValidDto(title: "My event"));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("My event", created.Title);
        Assert.NotEqual(default, created.StartAt);
        Assert.NotEqual(default, created.EndAt);
        Assert.True(created.EndAt > created.StartAt);
    }

    [Fact]
    public void GetAll_ShouldReturnAllEvents()
    {
        var service = new EventService();
        service.Create(ValidDto(title: "A", start: new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc)));
        service.Create(ValidDto(title: "B", start: new DateTime(2026, 06, 02, 10, 0, 0, DateTimeKind.Utc)));

        var result = service.GetAll();

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public void GetById_ShouldReturnEvent_WhenExists()
    {
        var service = new EventService();
        var created = service.Create(ValidDto(title: "Find me"));

        var found = service.GetById(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
    }

    [Fact]
    public void GetById_ShouldReturnNull_WhenNotExists()
    {
        var service = new EventService();

        var found = service.GetById(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public void Update_ShouldUpdateExistingEvent()
    {
        var service = new EventService();
        var created = service.Create(ValidDto(title: "Old"));

        var ok = service.Update(created.Id, ValidDto(title: "New"));

        Assert.True(ok);
        Assert.Equal("New", service.GetById(created.Id)!.Title);
    }

    [Fact]
    public void Update_ShouldReturnFalse_WhenEventNotExists()
    {
        var service = new EventService();

        var ok = service.Update(Guid.NewGuid(), ValidDto(title: "New"));

        Assert.False(ok);
    }

    [Fact]
    public void Delete_ShouldDeleteExistingEvent()
    {
        var service = new EventService();
        var created = service.Create(ValidDto(title: "To delete"));

        var ok = service.Delete(created.Id);

        Assert.True(ok);
        Assert.Null(service.GetById(created.Id));
    }

    [Fact]
    public void Delete_ShouldReturnFalse_WhenEventNotExists()
    {
        var service = new EventService();

        var ok = service.Delete(Guid.NewGuid());

        Assert.False(ok);
    }

    [Fact]
    public void Filter_ByTitle_ShouldBeCaseInsensitive_AndPartial()
    {
        var service = new EventService();
        service.Create(ValidDto(title: "DotNet Conf"));
        service.Create(ValidDto(title: "Java Meetup"));

        var result = service.GetAll(title: "conf");

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("DotNet Conf", result.Items.First().Title);
    }

    [Fact]
    public void Filter_ByDates_ShouldWork()
    {
        var service = new EventService();

        service.Create(ValidDto(
            title: "E1",
            start: new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 10, 11, 0, 0, DateTimeKind.Utc)));

        service.Create(ValidDto(
            title: "E2",
            start: new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 01, 11, 0, 0, DateTimeKind.Utc)));

        service.Create(ValidDto(
            title: "E3",
            start: new DateTime(2026, 06, 20, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 25, 11, 0, 0, DateTimeKind.Utc)));

        var from = new DateTime(2026, 06, 05, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 06, 15, 23, 59, 59, DateTimeKind.Utc);

        var result = service.GetAll(from: from, to: to);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("E1", result.Items.First().Title);
    }

    [Fact]
    public void Pagination_ShouldReturnCorrectPage()
    {
        var service = new EventService();

        for (int i = 1; i <= 12; i++)
        {
            service.Create(ValidDto(
                title: $"E{i}",
                start: new DateTime(2026, 06, i, 10, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 06, i, 11, 0, 0, DateTimeKind.Utc)));
        }

        var page2 = service.GetAll(page: 2, pageSize: 5);

        Assert.Equal(12, page2.TotalCount);
        Assert.Equal(2, page2.Page);
        Assert.Equal(5, page2.Count);

        Assert.Equal("E6", page2.Items.First().Title);
        Assert.Equal("E10", page2.Items.Last().Title);
    }

    [Fact]
    public void CombinedFiltering_ShouldWorkTogether()
    {
        var service = new EventService();

        service.Create(ValidDto(
            title: "Conf Moscow",
            start: new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 10, 11, 0, 0, DateTimeKind.Utc)));

        service.Create(ValidDto(
            title: "Conf SPB",
            start: new DateTime(2026, 07, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 07, 10, 11, 0, 0, DateTimeKind.Utc)));

        service.Create(ValidDto(
            title: "Meetup Moscow",
            start: new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 06, 10, 11, 0, 0, DateTimeKind.Utc)));

        var result = service.GetAll(
            title: "conf",
            from: new DateTime(2026, 06, 01, 0, 0, 0, DateTimeKind.Utc),
            to: new DateTime(2026, 06, 30, 23, 59, 59, DateTimeKind.Utc),
            page: 1,
            pageSize: 10);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Conf Moscow", result.Items.First().Title);
    }

    [Fact]
    public void Update_WithEndBeforeStart_ShouldThrowValidationException()
    {
        var service = new EventService();
        var id = service.Create(ValidDto()).Id;

        Assert.Throws<ValidationException>(() =>
            service.Update(id, ValidDto(
                start: new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2026, 06, 01, 9, 0, 0, DateTimeKind.Utc)
            )));
    }
}