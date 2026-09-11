using Events.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Events.Infrastructure.DataAccess;

public sealed class EventsDbContext : DbContext
{
    public EventsDbContext(DbContextOptions<EventsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();

    public DbSet<ProcessedBookingEvent> ProcessedBookingEvents => Set<ProcessedBookingEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
