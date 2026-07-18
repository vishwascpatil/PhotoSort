using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryFeedbackConfiguration : IEntityTypeConfiguration<MemoryFeedback>
{
    public void Configure(EntityTypeBuilder<MemoryFeedback> builder)
    {
        builder.ToTable("MemoryFeedback");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Feedback)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(f => f.Reason)
            .HasMaxLength(1024);

        builder.HasOne(f => f.Memory)
            .WithMany(m => m.FeedbackEntries)
            .HasForeignKey(f => f.MemoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.MemoryId);
        builder.HasIndex(f => f.CreatedAt);
    }
}
