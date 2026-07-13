using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class FaceConfiguration : IEntityTypeConfiguration<Face>
{
    public void Configure(EntityTypeBuilder<Face> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.BoundingBoxX)
            .IsRequired();

        builder.Property(f => f.BoundingBoxY)
            .IsRequired();

        builder.Property(f => f.BoundingBoxWidth)
            .IsRequired();

        builder.Property(f => f.BoundingBoxHeight)
            .IsRequired();

        builder.Property(f => f.Confidence)
            .IsRequired();

        builder.Property(f => f.ThumbnailPath)
            .HasMaxLength(2048);

        builder.HasIndex(f => f.PhotoId);
        builder.HasIndex(f => f.IsIgnored);
        builder.HasIndex(f => f.CreatedDate);
        builder.HasIndex(f => f.RecognitionState);
        builder.HasIndex(f => f.DetectionModelVersion);
        builder.HasIndex(f => f.FaceSize);

        builder.Property(f => f.RecognitionConfidence)
            .HasDefaultValue(0);

        builder.Property(f => f.LastRecognitionDate)
            .IsRequired(false);

        builder.HasIndex(f => f.LastRecognitionDate);

        builder.HasOne(f => f.Photo)
            .WithMany(p => p.Faces)
            .HasForeignKey(f => f.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.FaceEmbedding)
            .WithOne(e => e.Face)
            .HasForeignKey<FaceEmbedding>(e => e.FaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
