using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Configurations;

public class StudentRequestConfiguration : IEntityTypeConfiguration<StudentRequest>
{
    public void Configure(EntityTypeBuilder<StudentRequest> builder)
    {
        builder.ToTable("StudentRequests", "StudentServices");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId).IsRequired();
        builder.Property(x => x.ServiceId).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.PaymentStatus).IsRequired();
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.PaymentTransactionId).HasMaxLength(200);
        builder.Property(x => x.SubmittedData).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.CurrentStepOrder).IsRequired();

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.HistoryEntries)
            .WithOne(x => x.StudentRequest)
            .HasForeignKey(x => x.StudentRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => x.ServiceId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AssignedToStaffId);
        builder.HasIndex(x => x.SubmittedAt);
    }
}