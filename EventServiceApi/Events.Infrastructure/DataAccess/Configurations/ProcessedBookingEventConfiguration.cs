using Events.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Infrastructure.DataAccess.Configurations;

internal sealed class ProcessedBookingEventConfiguration : IEntityTypeConfiguration<ProcessedBookingEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedBookingEvent> builder)
    {
        builder.ToTable("processed_booking_events");

        // BookingId — естественный ключ идемпотентности: одна бронь не может быть учтена дважды.
        builder.HasKey(e => e.BookingId);

        builder.Property(e => e.BookingId)
               .ValueGeneratedNever();

        builder.Property(e => e.EventId)
               .IsRequired();

        builder.Property(e => e.ProcessedAt)
               .IsRequired();
    }
}
