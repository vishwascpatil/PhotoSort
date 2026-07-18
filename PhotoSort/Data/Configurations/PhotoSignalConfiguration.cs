using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class PhotoSignalConfiguration : IEntityTypeConfiguration<PhotoSignal>
{
    public void Configure(EntityTypeBuilder<PhotoSignal> builder)
    {
        builder.ToTable("PhotoSignals");

        builder.HasKey(s => s.PhotoId);

        builder.Property(s => s.Signals)
            .IsRequired();

        builder.Property(s => s.Version)
            .HasDefaultValue(1);
    }
}
