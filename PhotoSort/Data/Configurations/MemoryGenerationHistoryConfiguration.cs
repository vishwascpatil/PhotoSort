using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryGenerationHistoryConfiguration : IEntityTypeConfiguration<MemoryGenerationHistory>
{
    public void Configure(EntityTypeBuilder<MemoryGenerationHistory> builder)
    {
        builder.ToTable("MemoryGenerationHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Stage)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(h => h.MemoryTypeKey)
            .HasMaxLength(128);

        builder.Property(h => h.Error);

        builder.HasIndex(h => h.RunId);
        builder.HasIndex(h => h.StartedAt);
        builder.HasIndex(h => new { h.RunId, h.Stage });
    }
}
