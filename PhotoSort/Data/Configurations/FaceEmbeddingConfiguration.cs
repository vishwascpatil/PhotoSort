using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class FaceEmbeddingConfiguration : IEntityTypeConfiguration<FaceEmbedding>
{
    public void Configure(EntityTypeBuilder<FaceEmbedding> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.FaceId)
            .IsUnique();

        builder.Property(e => e.ModelVersion)
            .HasMaxLength(64);

        builder.Property(e => e.Confidence)
            .IsRequired();

        builder.HasOne(e => e.Face)
            .WithOne(f => f.FaceEmbedding)
            .HasForeignKey<FaceEmbedding>(e => e.FaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
