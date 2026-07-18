using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryItemConfiguration : IEntityTypeConfiguration<MemoryItem>
{
    public void Configure(EntityTypeBuilder<MemoryItem> builder)
    {
        builder.ToTable("MemoryItems");

        builder.HasKey(i => new { i.MemoryId, i.PhotoId });

        builder.Property(i => i.Role)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("supporting");

        builder.HasOne(i => i.Memory)
            .WithMany(m => m.Items)
            .HasForeignKey(i => i.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.PhotoId);
        builder.HasIndex(i => i.MemoryId);
        builder.HasIndex(i => new { i.MemoryId, i.SortOrder });
    }
}
