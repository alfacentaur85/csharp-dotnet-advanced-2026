using EventServiceApi.DataAccess;
using EventServiceApi.DataAccess.Repositories;
using EventServiceApi.Models;
using Microsoft.EntityFrameworkCore;

using EventApi.IntegrationTests.Fixtures;

namespace EventApi.IntegrationTests.Repositories;

public sealed class EventRepositoryTests : RepositoryTestBase
{
    public EventRepositoryTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    private static readonly DateTime BaseDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetPagedAsync_NoFilters_ReturnsAllEvents()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var events = new[]
        {
            MakeEvent("Tech Conference", startAt: BaseDate, endAt: BaseDate.AddHours(2)),
            MakeEvent("Music Festival", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2)),
            MakeEvent("Art Exhibition", startAt: BaseDate.AddDays(2), endAt: BaseDate.AddDays(2).AddHours(2)),
        };
        context.Events.AddRange(events);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(
            title: null, from: null, to: null, page: 1, pageSize: 10);

        Assert.Equal(3, totalCount);
        Assert.Equal(3, items.Count);
    }

    [Theory]
    [InlineData("conf", 1)]
    [InlineData("CONFERENCE", 1)]
    [InlineData("fest", 1)]
    [InlineData("nomatch", 0)]
    public async Task GetPagedAsync_TitleFilter_IsCaseInsensitivePartialMatch(string titleFilter, int expectedCount)
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        context.Events.AddRange(
            MakeEvent("Tech Conference", startAt: BaseDate, endAt: BaseDate.AddHours(2)),
            MakeEvent("Music Festival", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2)));
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(
            title: titleFilter, from: null, to: null, page: 1, pageSize: 10);

        Assert.Equal(expectedCount, totalCount);
        Assert.Equal(expectedCount, items.Count);
    }

    [Fact]
    public async Task GetPagedAsync_FromFilter_ReturnsEventsStartingOnOrAfter()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var early = MakeEvent("Early", startAt: BaseDate, endAt: BaseDate.AddHours(2));
        var boundary = MakeEvent("Boundary", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2));
        var later = MakeEvent("Later", startAt: BaseDate.AddDays(2), endAt: BaseDate.AddDays(2).AddHours(2));
        context.Events.AddRange(early, boundary, later);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(
            title: null, from: BaseDate.AddDays(1), to: null, page: 1, pageSize: 10);

        Assert.Equal(2, totalCount);
        Assert.DoesNotContain(items, e => e.Id == early.Id);
        Assert.Contains(items, e => e.Id == boundary.Id);
        Assert.Contains(items, e => e.Id == later.Id);
    }

    [Fact]
    public async Task GetPagedAsync_ToFilter_ReturnsEventsEndingOnOrBefore()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var early = MakeEvent("Early", startAt: BaseDate, endAt: BaseDate.AddHours(2));
        var boundary = MakeEvent("Boundary", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2));
        var later = MakeEvent("Later", startAt: BaseDate.AddDays(2), endAt: BaseDate.AddDays(2).AddHours(2));
        context.Events.AddRange(early, boundary, later);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(
            title: null, from: null, to: boundary.EndAt, page: 1, pageSize: 10);

        Assert.Equal(2, totalCount);
        Assert.Contains(items, e => e.Id == early.Id);
        Assert.Contains(items, e => e.Id == boundary.Id);
        Assert.DoesNotContain(items, e => e.Id == later.Id);
    }

    [Fact]
    public async Task GetPagedAsync_CombinedTitleAndDateFilters_IntersectsResults()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var matches = MakeEvent("Tech Conference", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2));
        var wrongTitle = MakeEvent("Music Festival", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2));
        var wrongDate = MakeEvent("Tech Conference", startAt: BaseDate.AddDays(10), endAt: BaseDate.AddDays(10).AddHours(2));
        context.Events.AddRange(matches, wrongTitle, wrongDate);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(
            title: "tech",
            from: BaseDate,
            to: BaseDate.AddDays(5),
            page: 1,
            pageSize: 10);

        Assert.Equal(1, totalCount);
        Assert.Equal(matches.Id, Assert.Single(items).Id);
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsDistinctPagesWithCorrectTotalCount()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var events = Enumerable.Range(0, 5)
            .Select(i => MakeEvent($"Event {i}", startAt: BaseDate.AddDays(i), endAt: BaseDate.AddDays(i).AddHours(2)))
            .ToList();
        context.Events.AddRange(events);
        await context.SaveChangesAsync();

        var (page1Items, page1Total) = await repository.GetPagedAsync(
            title: null, from: null, to: null, page: 1, pageSize: 2);
        var (page2Items, page2Total) = await repository.GetPagedAsync(
            title: null, from: null, to: null, page: 2, pageSize: 2);

        Assert.Equal(5, page1Total);
        Assert.Equal(5, page2Total);
        Assert.Equal(2, page1Items.Count);
        Assert.Equal(2, page2Items.Count);
        Assert.Empty(page1Items.Select(e => e.Id).Intersect(page2Items.Select(e => e.Id)));
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondData_ReturnsEmptyItemsWithCorrectTotalCount()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        context.Events.AddRange(
            MakeEvent("A", startAt: BaseDate, endAt: BaseDate.AddHours(2)),
            MakeEvent("B", startAt: BaseDate.AddDays(1), endAt: BaseDate.AddDays(1).AddHours(2)));
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.GetPagedAsync(
            title: null, from: null, to: null, page: 5, pageSize: 10);

        Assert.Equal(2, totalCount);
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetPagedAsync_OrdersByStartAtThenById()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var sameStart = BaseDate.AddDays(1);
        var events = Enumerable.Range(0, 3)
            .Select(_ => MakeEvent("Same start", startAt: sameStart, endAt: sameStart.AddHours(2)))
            .ToList();
        var earliest = MakeEvent("Earliest", startAt: BaseDate, endAt: BaseDate.AddHours(2));

        context.Events.AddRange(events.Concat([earliest]));
        await context.SaveChangesAsync();

        // Ожидаемый порядок тай-брейка по Id вычисляем тем же ORDER BY на стороне БД,
        // а не через Guid.CompareTo — сравнение GUID в .NET и порядок байт uuid в PostgreSQL не совпадают.
        var expectedSameStartOrder = await context.Events
            .Where(e => e.StartAt == sameStart)
            .OrderBy(e => e.Id)
            .Select(e => e.Id)
            .ToListAsync();

        var (items, _) = await repository.GetPagedAsync(
            title: null, from: null, to: null, page: 1, pageSize: 10);

        Assert.Equal(earliest.Id, items[0].Id);
        Assert.Equal(expectedSameStartOrder, items.Skip(1).Select(e => e.Id));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUntrackedEvent()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var evt = MakeEvent("Tech Conference");
        context.Events.Add(evt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByIdAsync(evt.Id);

        Assert.NotNull(result);
        Assert.Equal(evt.Id, result!.Id);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdTrackedAsync_ExistingId_ReturnsTrackedEvent()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var evt = MakeEvent("Tech Conference");
        context.Events.Add(evt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByIdTrackedAsync(evt.Id);

        Assert.NotNull(result);
        var entry = context.Entry(result!);
        Assert.Equal(EntityState.Unchanged, entry.State);
    }

    [Fact]
    public async Task GetByIdTrackedAsync_MissingId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new EventRepository(context);

        var result = await repository.GetByIdTrackedAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Add_ThenSaveChanges_PersistsEventVisibleFromNewContext()
    {
        await using var writeContext = CreateContext();
        var repository = new EventRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var evt = MakeEvent("Persisted Event");
        repository.Add(evt);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persisted = await readContext.Events.FindAsync(evt.Id);

        Assert.NotNull(persisted);
        Assert.Equal("Persisted Event", persisted!.Title);
    }

    [Fact]
    public async Task Remove_ThenSaveChanges_DeletesEventFromNewContext()
    {
        await using var writeContext = CreateContext();
        var evt = MakeEvent("To Delete");
        writeContext.Events.Add(evt);
        await writeContext.SaveChangesAsync();
        writeContext.ChangeTracker.Clear();

        var repository = new EventRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var tracked = await repository.GetByIdTrackedAsync(evt.Id);
        repository.Remove(tracked!);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persisted = await readContext.Events.FindAsync(evt.Id);

        Assert.Null(persisted);
    }
}
