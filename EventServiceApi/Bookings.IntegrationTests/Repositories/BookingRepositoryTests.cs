using Bookings.Domain.Enums;
using Bookings.Infrastructure.DataAccess;
using Bookings.Infrastructure.DataAccess.Repositories;
using Bookings.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace Bookings.IntegrationTests.Repositories;

public sealed class BookingRepositoryTests : RepositoryTestBase
{
    public BookingRepositoryTests(PostgresContainerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsUntrackedBooking()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        var booking = MakeBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByIdAsync(booking.Id);

        Assert.NotNull(result);
        Assert.Equal(booking.Id, result!.Id);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdTrackedAsync_ExistingId_ReturnsTrackedBooking()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        var booking = MakeBooking();
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await repository.GetByIdTrackedAsync(booking.Id);

        Assert.NotNull(result);
        var entry = context.Entry(result!);
        Assert.Equal(EntityState.Unchanged, entry.State);
    }

    [Fact]
    public async Task GetByIdTrackedAsync_MissingId_ReturnsNull()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        var result = await repository.GetByIdTrackedAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetPendingAsync_ReturnsOnlyPendingBookings_OrderedByCreatedAt()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var older = MakeBooking(eventId, userId, BookingStatus.Pending, createdAt: DateTime.UtcNow.AddMinutes(-10));
        var newer = MakeBooking(eventId, userId, BookingStatus.Pending, createdAt: DateTime.UtcNow.AddMinutes(-1));
        var confirmed = MakeBooking(eventId, userId, BookingStatus.Confirmed, createdAt: DateTime.UtcNow.AddMinutes(-5));
        var rejected = MakeBooking(eventId, userId, BookingStatus.Rejected, createdAt: DateTime.UtcNow.AddMinutes(-5));

        context.Bookings.AddRange(newer, older, confirmed, rejected);
        await context.SaveChangesAsync();

        var pending = await repository.GetPendingAsync();

        Assert.Equal(2, pending.Count);
        Assert.Equal([older.Id, newer.Id], pending.Select(b => b.Id));
    }

    [Fact]
    public async Task GetPendingAsync_NoPendingBookings_ReturnsEmpty()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        context.Bookings.Add(MakeBooking(status: BookingStatus.Confirmed));
        await context.SaveChangesAsync();

        var pending = await repository.GetPendingAsync();

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Add_ThenSaveChanges_PersistsBookingWithStatusVisibleFromNewContext()
    {
        await using var writeContext = CreateContext();
        var repository = new BookingRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var eventId = Guid.NewGuid();
        var booking = MakeBooking(eventId, status: BookingStatus.Pending);
        repository.Add(booking);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persisted = await readContext.Bookings.FindAsync(booking.Id);

        Assert.NotNull(persisted);
        Assert.Equal(BookingStatus.Pending, persisted!.Status);
        Assert.Equal(eventId, persisted.EventId);
    }

    [Fact]
    public async Task Remove_ThenSaveChanges_DeletesBooking()
    {
        await using var writeContext = CreateContext();
        var booking = MakeBooking();
        writeContext.Bookings.Add(booking);
        await writeContext.SaveChangesAsync();
        writeContext.ChangeTracker.Clear();

        var repository = new BookingRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var tracked = await repository.GetByIdTrackedAsync(booking.Id);
        repository.Remove(tracked!);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persisted = await readContext.Bookings.FindAsync(booking.Id);

        Assert.Null(persisted);
    }

    [Fact]
    public async Task CountActiveByUserAsync_CountsOnlyPendingAndConfirmed_ForGivenUser()
    {
        await using var context = CreateContext();
        var repository = new BookingRepository(context);

        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        context.Bookings.AddRange(
            MakeBooking(userId: userId, status: BookingStatus.Pending),
            MakeBooking(userId: userId, status: BookingStatus.Confirmed),
            MakeBooking(userId: userId, status: BookingStatus.Cancelled),
            MakeBooking(userId: userId, status: BookingStatus.Rejected),
            MakeBooking(userId: otherUserId, status: BookingStatus.Pending));
        await context.SaveChangesAsync();

        var count = await repository.CountActiveByUserAsync(userId);

        Assert.Equal(2, count);
    }
}
