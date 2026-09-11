using Events.Application.Interfaces;
using Events.Domain.Entities;

namespace Events.Infrastructure.DataAccess.Repositories;

public sealed class ProcessedBookingEventStore : IProcessedBookingEventStore
{
    private readonly EventsDbContext _context;

    public ProcessedBookingEventStore(EventsDbContext context)
    {
        _context = context;
    }

    public void MarkProcessed(Guid bookingId, Guid eventId)
    {
        _context.ProcessedBookingEvents.Add(new ProcessedBookingEvent
        {
            BookingId = bookingId,
            EventId = eventId,
            ProcessedAt = DateTime.UtcNow
        });
    }
}
