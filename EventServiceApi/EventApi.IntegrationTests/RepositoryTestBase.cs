using EventApi.IntegrationTests.Fixtures;
using EventService.Infrastructure.DataAccess;
using EventService.Domain.Enums;
using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests;

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

    protected AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new AppDbContext(options);
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

    protected static User MakeUser(
        string login = "user",
        string passwordHash = "hash",
        UserRole role = UserRole.User)
        => User.Create(login, passwordHash, role);

    protected static Booking MakeBooking(
        Guid eventId,
        Guid userId,
        BookingStatus status = BookingStatus.Pending,
        DateTime? createdAt = null,
        DateTime? processedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ProcessedAt = processedAt
        };
}
