using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryScoreConfiguration : IEntityTypeConfiguration<MemoryScore>
{
    public void Configure(EntityTypeBuilder<MemoryScore> builder)
    {
        builder.ToTable("MemoryScores");

        builder.HasKey(s => new { s.MemoryId, s.PhotoId });

        builder.HasOne(s => s.Memory)
            .WithMany(m => m.Scores)
            .HasForeignKey(s => s.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CalculatedAt);
        builder.HasIndex(s => s.PhotoId);
    }
}
