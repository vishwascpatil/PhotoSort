using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryTypeFamilyConfiguration : IEntityTypeConfiguration<MemoryTypeFamily>
{
    public void Configure(EntityTypeBuilder<MemoryTypeFamily> builder)
    {
        builder.ToTable("MemoryTypeFamilies");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(f => f.Icon)
            .HasMaxLength(64);

        builder.HasIndex(f => f.Name).IsUnique();

        builder.HasIndex(f => f.SortOrder);
    }
}
