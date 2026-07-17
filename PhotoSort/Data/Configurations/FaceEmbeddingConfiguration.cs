using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        builder.Property(e => e.Embedding)
            .HasConversion(new ValueConverter<float[], string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? new float[0]))
            .HasColumnType("TEXT");

        builder.HasOne(e => e.Face)
            .WithOne(f => f.FaceEmbedding)
            .HasForeignKey<FaceEmbedding>(e => e.FaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
