using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryCacheEntryConfiguration : IEntityTypeConfiguration<MemoryCacheEntry>
{
    public void Configure(EntityTypeBuilder<MemoryCacheEntry> builder)
    {
        builder.ToTable("MemoryCache");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("candidate");

        builder.Property(e => e.MemoryTypeKey)
            .HasMaxLength(128);

        builder.Property(e => e.PhotoIds)
            .IsRequired();

        builder.Property(e => e.Metadata);

        builder.HasIndex(e => e.ExpiresAt);
        builder.HasIndex(e => e.Type);
    }
}
