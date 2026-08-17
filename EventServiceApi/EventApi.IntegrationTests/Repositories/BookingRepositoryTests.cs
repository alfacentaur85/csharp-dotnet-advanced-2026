using EventApi.IntegrationTests.Fixtures;
using EventService.Infrastructure.DataAccess;
using EventService.Infrastructure.DataAccess.Repositories;
using EventService.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EventApi.IntegrationTests.Repositories;

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

        var evt = MakeEvent("Conference");
        context.Events.Add(evt);
        var user = MakeUser();
        context.Users.Add(user);
        var booking = MakeBooking(evt.Id, user.Id);
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

        var evt = MakeEvent("Conference");
        context.Events.Add(evt);
        var user = MakeUser();
        context.Users.Add(user);
        var booking = MakeBooking(evt.Id, user.Id);
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

        var evt = MakeEvent("Conference");
        context.Events.Add(evt);
        var user = MakeUser();
        context.Users.Add(user);

        var older = MakeBooking(evt.Id, user.Id, BookingStatus.Pending, createdAt: DateTime.UtcNow.AddMinutes(-10));
        var newer = MakeBooking(evt.Id, user.Id, BookingStatus.Pending, createdAt: DateTime.UtcNow.AddMinutes(-1));
        var confirmed = MakeBooking(evt.Id, user.Id, BookingStatus.Confirmed, createdAt: DateTime.UtcNow.AddMinutes(-5));
        var rejected = MakeBooking(evt.Id, user.Id, BookingStatus.Rejected, createdAt: DateTime.UtcNow.AddMinutes(-5));

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

        var evt = MakeEvent("Conference");
        context.Events.Add(evt);
        var user = MakeUser();
        context.Users.Add(user);
        context.Bookings.Add(MakeBooking(evt.Id, user.Id, BookingStatus.Confirmed));
        await context.SaveChangesAsync();

        var pending = await repository.GetPendingAsync();

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Add_ThenSaveChanges_PersistsBookingWithStatusVisibleFromNewContext()
    {
        await using var writeContext = CreateContext();
        var evt = MakeEvent("Conference");
        writeContext.Events.Add(evt);
        var user = MakeUser();
        writeContext.Users.Add(user);
        await writeContext.SaveChangesAsync();

        var repository = new BookingRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var booking = MakeBooking(evt.Id, user.Id, BookingStatus.Pending);
        repository.Add(booking);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persisted = await readContext.Bookings.FindAsync(booking.Id);

        Assert.NotNull(persisted);
        Assert.Equal(BookingStatus.Pending, persisted!.Status);
        Assert.Equal(evt.Id, persisted.EventId);
    }

    [Fact]
    public async Task RemovingEvent_CascadeDeletesRelatedBookings()
    {
        await using var writeContext = CreateContext();
        var evt = MakeEvent("Conference");
        writeContext.Events.Add(evt);
        var user = MakeUser();
        writeContext.Users.Add(user);
        var booking = MakeBooking(evt.Id, user.Id);
        writeContext.Bookings.Add(booking);
        await writeContext.SaveChangesAsync();
        writeContext.ChangeTracker.Clear();

        var eventRepository = new EventRepository(writeContext);
        var unitOfWork = new UnitOfWork(writeContext);

        var trackedEvent = await eventRepository.GetByIdTrackedAsync(evt.Id);
        eventRepository.Remove(trackedEvent!);
        await unitOfWork.SaveChangesAsync();

        await using var readContext = CreateContext();
        var persistedBooking = await readContext.Bookings.FindAsync(booking.Id);

        Assert.Null(persistedBooking);
    }
}
