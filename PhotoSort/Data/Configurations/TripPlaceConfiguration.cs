using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class TripPlaceConfiguration : IEntityTypeConfiguration<TripPlace>
{
    public void Configure(EntityTypeBuilder<TripPlace> builder)
    {
        builder.HasKey(tp => tp.Id);

        builder.HasIndex(tp => new { tp.TripId, tp.PlaceId })
            .IsUnique();

        builder.HasIndex(tp => tp.TripId);
        builder.HasIndex(tp => tp.PlaceId);

        builder.HasOne(tp => tp.Trip)
            .WithMany(t => t.TripPlaces)
            .HasForeignKey(tp => tp.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tp => tp.Place)
            .WithMany()
            .HasForeignKey(tp => tp.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
