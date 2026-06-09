using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(
        EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.StudentCode)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.NationalId)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.NationalId)
            .IsUnique();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(30);

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.HasIndex(x => x.Email);

        builder.Property(x => x.Gender)
            .HasMaxLength(20);

        builder.Property(x => x.PhotoUrl)
            .HasMaxLength(500);

        builder.Property(x => x.GuardianName)
            .HasMaxLength(200);

        builder.Property(x => x.GuardianPhone)
            .HasMaxLength(30);

        builder.HasOne(x => x.StructureNode)
            .WithMany()
            .HasForeignKey(x => x.StructureNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.StructureNodeId);

        // ExternallySourced is a composed data block (not a base class); EF
        // flattens it onto the Students table via OwnsOne with explicit column
        // names so the underlying schema is identical to the inheritance-era
        // layout — no migration needed for the composition refactor.
        // Populated by the sync write gateway when this row was sourced
        // upstream. Filtered unique index keeps sync-sourced rows globally
        // unique on ExternalId while allowing Core-created rows (ExternalId = NULL)
        // to coexist.
        builder.OwnsOne(x => x.ExternallySourced, ec =>
        {
            ec.Property(p => p.ExternalId).HasColumnName("ExternalId").HasMaxLength(128);
            ec.Property(p => p.ExternalUpdatedAt).HasColumnName("ExternalUpdatedAt");
            ec.Property(p => p.ExternalVersion).HasColumnName("ExternalVersion");
            ec.Property(p => p.LastSyncedAt).HasColumnName("LastSyncedAt");
            ec.Property(p => p.OriginSystem).HasColumnName("OriginSystem").HasMaxLength(32).IsRequired();
            ec.HasIndex(p => p.ExternalId)
                .IsUnique()
                .HasFilter("[ExternalId] IS NOT NULL");
        });

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}