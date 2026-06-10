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

        builder.Property(x => x.RequestNumber)
            .IsRequired()
            .ValueGeneratedOnAdd()
            .UseIdentityColumn(seed: 1, increment: 1);

        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.PaymentStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.AmountPaid).HasPrecision(18, 2);
        builder.Property(x => x.SubmittedData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasMany(x => x.HistoryEntries)
            .WithOne(x => x.StudentRequest)
            .HasForeignKey(x => x.StudentRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.StudentId);
        builder.HasIndex(x => x.ServiceId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.AssignedToStaffId);
    }
}