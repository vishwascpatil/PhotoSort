using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryStatisticsConfiguration : IEntityTypeConfiguration<MemoryStatistics>
{
    public void Configure(EntityTypeBuilder<MemoryStatistics> builder)
    {
        builder.ToTable("MemoryStatistics");

        builder.HasKey(s => s.MemoryTypeId);

        builder.HasOne(s => s.MemoryType)
            .WithMany()
            .HasForeignKey(s => s.MemoryTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.UpdatedAt);
    }
}
