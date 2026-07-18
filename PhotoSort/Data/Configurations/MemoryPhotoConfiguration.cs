using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryPhotoConfiguration : IEntityTypeConfiguration<MemoryPhoto>
{
    public void Configure(EntityTypeBuilder<MemoryPhoto> builder)
    {
        builder.ToTable("MemoryPhotos");

        builder.HasKey(p => new { p.MemoryId, p.PhotoId });

        builder.Property(p => p.Role)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("Supporting");

        builder.HasOne(p => p.Memory)
            .WithMany(m => m.Photos)
            .HasForeignKey(p => p.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.PhotoId);
        builder.HasIndex(p => new { p.MemoryId, p.SortOrder });
    }
}
