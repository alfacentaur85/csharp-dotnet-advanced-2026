using EventServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventServiceApi.DataAccess.Configurations;

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);

        // Id генерируется в коде (Guid.NewGuid()), БД не генерирует
        builder.Property(e => e.Id)
               .ValueGeneratedNever();

        builder.Property(e => e.Title)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(e => e.Description)
               .HasMaxLength(2000);

        builder.Property(e => e.StartAt)
               .IsRequired();

        builder.Property(e => e.EndAt)
               .IsRequired();

        builder.Property(e => e.TotalSeats)
               .IsRequired();

        builder.Property(e => e.AvailableSeats)
               .IsRequired();

        builder.HasMany(e => e.Bookings)
               .WithOne(b => b.Event)
               .HasForeignKey(b => b.EventId);
    }
}