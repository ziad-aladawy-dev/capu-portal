using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class ServiceFieldValueConfiguration : IEntityTypeConfiguration<ServiceFieldValue>
{
    public void Configure(EntityTypeBuilder<ServiceFieldValue> builder)
    {
        builder.ToTable("ServiceFieldValues");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentServiceRequestId).IsRequired();
        builder.Property(x => x.FieldDefinitionId).IsRequired();
        builder.Property(x => x.Value).HasMaxLength(4000).IsRequired();

        // Cascade delete from request side is configured on
        // StudentServiceRequestConfiguration. Configure the
        // ServiceFieldDefinition side with Restrict — the admin should not
        // be able to delete a field definition that historical submissions
        // still reference.
        builder.HasOne(x => x.FieldDefinition)
            .WithMany()
            .HasForeignKey(x => x.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // One value per (request, field). The composite uniqueness lets the
        // upsert path on resubmission be a clean conflict-aware merge.
        builder.HasIndex(x => new { x.StudentServiceRequestId, x.FieldDefinitionId }).IsUnique();
    }
}
