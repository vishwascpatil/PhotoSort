using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PhotoSort.Models;

namespace PhotoSort.Data.Configurations;

public sealed class PersonFaceConfiguration : IEntityTypeConfiguration<PersonFace>
{
    public void Configure(EntityTypeBuilder<PersonFace> builder)
    {
        builder.HasKey(pf => pf.Id);

        builder.HasIndex(pf => new { pf.PersonId, pf.FaceId }).IsUnique();
        builder.HasIndex(pf => pf.PersonId);
        builder.HasIndex(pf => pf.FaceId);
        builder.HasIndex(pf => pf.AssignedDate);

        builder.HasOne(pf => pf.Person)
            .WithMany(p => p.PersonFaces)
            .HasForeignKey(pf => pf.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pf => pf.Face)
            .WithMany(f => f.PersonFaces)
            .HasForeignKey(pf => pf.FaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
