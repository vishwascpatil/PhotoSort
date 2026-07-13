using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(p => p.ThumbnailPath)
            .HasMaxLength(2048);

        builder.Property(p => p.FaceCount)
            .HasDefaultValue(0);

        builder.Property(p => p.PhotoCount)
            .HasDefaultValue(0);

        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.FaceCount);
        builder.HasIndex(p => p.PhotoCount);
        builder.HasIndex(p => p.LastSeenDate);
        builder.HasIndex(p => p.CreatedDate);
    }
}
