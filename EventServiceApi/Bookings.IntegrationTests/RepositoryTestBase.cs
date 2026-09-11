using Bookings.Domain.Entities;
using Bookings.Domain.Enums;
using Bookings.IntegrationTests.Fixtures;
using Bookings.Infrastructure.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace Bookings.IntegrationTests;

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

    protected BookingsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new BookingsDbContext(options);
    }

    protected static Booking MakeBooking(
        Guid? eventId = null,
        Guid? userId = null,
        BookingStatus status = BookingStatus.Pending,
        DateTime? createdAt = null,
        DateTime? processedAt = null)
        => new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId ?? Guid.NewGuid(),
            UserId = userId ?? Guid.NewGuid(),
            Status = status,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ProcessedAt = processedAt
        };
}
