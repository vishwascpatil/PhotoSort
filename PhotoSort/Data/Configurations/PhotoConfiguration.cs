using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class PhotoConfiguration : IEntityTypeConfiguration<Photo>
{
    public void Configure(EntityTypeBuilder<Photo> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FilePath)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(p => p.FileName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(p => p.Extension)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(p => p.ThumbnailPath)
            .HasMaxLength(2048);

        builder.Property(p => p.ThumbnailSmallPath)
            .HasMaxLength(2048);

        builder.Property(p => p.ThumbnailMediumPath)
            .HasMaxLength(2048);

        builder.Property(p => p.PreviewClipPath)
            .HasMaxLength(2048);

        builder.Property(p => p.CameraMake)
            .HasMaxLength(128);

        builder.Property(p => p.CameraModel)
            .HasMaxLength(128);

        builder.HasIndex(p => p.FilePath)
            .IsUnique();

        builder.HasIndex(p => p.DateTaken);
        builder.HasIndex(p => p.FolderId);
        builder.HasIndex(p => p.IsFavorite);
        builder.HasIndex(p => p.State);
        builder.HasIndex(p => new { p.FolderId, p.State });
        builder.HasIndex(p => new { p.FolderId, p.State, p.ModifiedDateUtc });

        builder.HasIndex(p => p.ThumbnailGeneratedDate);

        builder.Property(p => p.ContentHash)
            .HasMaxLength(64);

        builder.Property(p => p.DuplicateGroupId);

        builder.HasIndex(p => p.ContentHash);
        builder.HasIndex(p => p.DuplicateGroupId);

        builder.Property(p => p.MediaCategory);

        builder.Property(p => p.ClassificationConfidence);

        builder.HasIndex(p => p.MediaCategory);

        builder.Property(p => p.PerceptualHash);

        builder.HasIndex(p => p.PerceptualHash);

        builder.Property(p => p.SimilarPhotoGroupId);

        builder.HasIndex(p => p.SimilarPhotoGroupId);

        builder.HasIndex(p => p.Latitude);
        builder.HasIndex(p => p.Longitude);
        builder.HasIndex(p => new { p.Latitude, p.Longitude });

        builder.HasOne(p => p.Folder)
            .WithMany(f => f.Photos)
            .HasForeignKey(p => p.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
