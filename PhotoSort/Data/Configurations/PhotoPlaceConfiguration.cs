using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class PhotoPlaceConfiguration : IEntityTypeConfiguration<PhotoPlace>
{
    public void Configure(EntityTypeBuilder<PhotoPlace> builder)
    {
        builder.HasKey(pp => new { pp.PhotoId, pp.PlaceId });

        builder.HasOne(pp => pp.Photo)
            .WithMany(p => p.PhotoPlaces)
            .HasForeignKey(pp => pp.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pp => pp.Place)
            .WithMany(p => p.PhotoPlaces)
            .HasForeignKey(pp => pp.PlaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
