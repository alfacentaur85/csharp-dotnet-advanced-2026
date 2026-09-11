using Events.IntegrationTests.Fixtures;
using Events.Infrastructure.DataAccess;
using Events.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Events.IntegrationTests;

[Collection(PostgresCollection.Name)]
public abstract class RepositoryTestBase : IAsyncLifetime
{
    private readonly string _connectionString;

    protected RepositoryTestBase(PostgresContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected EventsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new EventsDbContext(options);
    }

    protected static Event MakeEvent(
        string title = "Event",
        string? description = null,
        DateTime? startAt = null,
        DateTime? endAt = null,
        int totalSeats = 10)
        => Event.Create(
            title,
            description,
            startAt ?? DateTime.UtcNow.AddDays(1),
            endAt ?? DateTime.UtcNow.AddDays(1).AddHours(2),
            totalSeats);
}
