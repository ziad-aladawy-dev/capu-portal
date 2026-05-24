using CapitalUniversity.Modules.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.StudentServices.Persistence.Configurations;

public class ServiceDocumentSubmissionConfiguration : IEntityTypeConfiguration<ServiceDocumentSubmission>
{
    public void Configure(EntityTypeBuilder<ServiceDocumentSubmission> builder)
    {
        builder.ToTable("ServiceDocumentSubmissions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentServiceRequestId).IsRequired();
        builder.Property(x => x.DocumentDefinitionId).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.FileSize).IsRequired();

        builder.HasOne(x => x.DocumentDefinition)
            .WithMany()
            .HasForeignKey(x => x.DocumentDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.StudentServiceRequestId, x.DocumentDefinitionId });
    }
}
