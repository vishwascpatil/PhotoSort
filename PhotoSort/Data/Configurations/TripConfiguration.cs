using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(t => t.Notes)
            .HasMaxLength(4096);

        builder.Property(t => t.CoverPhotoPath)
            .HasMaxLength(2048);

        builder.HasIndex(t => t.Name);
        builder.HasIndex(t => t.StartDate);
        builder.HasIndex(t => t.EndDate);
        builder.HasIndex(t => t.IsFavorite);
        builder.HasIndex(t => new { t.StartDate, t.EndDate });
    }
}
