using EventService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventService.Infrastructure.DataAccess.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
               .ValueGeneratedNever();

        builder.Property(u => u.Login)
               .IsRequired()
               .HasMaxLength(200);

        builder.HasIndex(u => u.Login)
               .IsUnique();

        builder.Property(u => u.PasswordHash)
               .IsRequired();

        builder.Property(u => u.Role)
               .IsRequired()
               .HasConversion<string>();

        builder.HasMany(u => u.Bookings)
               .WithOne(b => b.User)
               .HasForeignKey(b => b.UserId);
    }
}
