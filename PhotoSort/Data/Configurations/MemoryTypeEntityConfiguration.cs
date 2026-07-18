using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryTypeEntityConfiguration : IEntityTypeConfiguration<MemoryTypeEntity>
{
    public void Configure(EntityTypeBuilder<MemoryTypeEntity> builder)
    {
        builder.ToTable("MemoryTypes");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Key)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(t => t.Icon)
            .HasMaxLength(64);

        builder.Property(t => t.Tone)
            .HasMaxLength(64);

        builder.Property(t => t.DefaultCoverStrategy)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("single");

        builder.Property(t => t.SeasonalMonths)
            .HasMaxLength(64);

        builder.HasOne(t => t.Family)
            .WithMany(f => f.MemoryTypes)
            .HasForeignKey(t => t.FamilyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.Key).IsUnique();
        builder.HasIndex(t => t.FamilyId);
        builder.HasIndex(t => t.SortOrder);
        builder.HasIndex(t => t.IsActive);
    }
}
