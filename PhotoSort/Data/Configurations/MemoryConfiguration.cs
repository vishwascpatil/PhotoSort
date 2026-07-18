using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryConfiguration : IEntityTypeConfiguration<Memory>
{
    public void Configure(EntityTypeBuilder<Memory> builder)
    {
        builder.ToTable("Memories");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedOnAdd();

        builder.Property(m => m.Title)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(m => m.Subtitle)
            .HasMaxLength(1024);

        builder.Property(m => m.StorySummary)
            .HasMaxLength(2048);

        builder.Property(m => m.CoverThumbnailPath)
            .HasMaxLength(2048);

        builder.Property(m => m.LocationSummary)
            .HasMaxLength(512);

        builder.Property(m => m.PeopleSummary)
            .HasMaxLength(512);

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasOne(m => m.MemoryTypeEntity)
            .WithMany(t => t.Memories)
            .HasForeignKey(m => m.MemoryTypeEntityId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(m => m.Items)
            .WithOne(i => i.Memory)
            .HasForeignKey(i => i.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Scores)
            .WithOne(s => s.Memory)
            .HasForeignKey(s => s.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.FeedbackEntries)
            .WithOne(f => f.Memory)
            .HasForeignKey(f => f.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(m => m.PersonIds);

        builder.HasIndex(m => m.DateStart);
        builder.HasIndex(m => m.DateEnd);
        builder.HasIndex(m => m.Score);
        builder.HasIndex(m => m.MemoryTypeEntityId);
        builder.HasIndex(m => m.IsGenerated);
        builder.HasIndex(m => new { m.IsArchived, m.Dismissed });
        builder.HasIndex(m => m.LastShownAt);
        builder.HasIndex(m => m.GeneratedAt);
    }
}
