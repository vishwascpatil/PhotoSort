using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class MemoryPreferencesConfiguration : IEntityTypeConfiguration<MemoryPreferences>
{
    public void Configure(EntityTypeBuilder<MemoryPreferences> builder)
    {
        builder.ToTable("MemoryPreferences");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PreferredTypes);

        builder.Property(p => p.ExcludedTypes);

        builder.Property(p => p.WeekdayMode)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("balanced");

        builder.Property(p => p.MusicPreference)
            .IsRequired()
            .HasMaxLength(32)
            .HasDefaultValue("none");
    }
}
