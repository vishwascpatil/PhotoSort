using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class TripPhotoConfiguration : IEntityTypeConfiguration<TripPhoto>
{
    public void Configure(EntityTypeBuilder<TripPhoto> builder)
    {
        builder.HasKey(tp => tp.Id);

        builder.HasIndex(tp => new { tp.TripId, tp.PhotoId })
            .IsUnique();

        builder.HasIndex(tp => tp.TripId);
        builder.HasIndex(tp => tp.PhotoId);
        builder.HasIndex(tp => tp.DateTaken);

        builder.HasOne(tp => tp.Trip)
            .WithMany(t => t.TripPhotos)
            .HasForeignKey(tp => tp.TripId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tp => tp.Photo)
            .WithMany()
            .HasForeignKey(tp => tp.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
