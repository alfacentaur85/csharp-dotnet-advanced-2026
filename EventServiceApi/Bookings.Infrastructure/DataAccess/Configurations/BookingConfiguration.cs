using Bookings.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Bookings.Infrastructure.DataAccess.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
               .ValueGeneratedNever();

        // EventId/UserId — обычные Guid-колонки без FK: Events и Users — отдельные сервисы/БД.
        builder.Property(b => b.EventId)
               .IsRequired();

        builder.Property(b => b.UserId)
               .IsRequired();

        builder.Property(b => b.Status)
               .IsRequired()
               .HasConversion<string>();

        builder.Property(b => b.CreatedAt)
               .IsRequired();

        builder.Property(b => b.ProcessedAt);
    }
}
