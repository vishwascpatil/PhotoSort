using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FolderPath)
            .IsRequired()
            .HasMaxLength(1024);

        builder.HasIndex(f => f.FolderPath)
            .IsUnique();

        builder.HasIndex(f => f.AddedDate);
        builder.HasIndex(f => f.LastScanDate);
    }
}
