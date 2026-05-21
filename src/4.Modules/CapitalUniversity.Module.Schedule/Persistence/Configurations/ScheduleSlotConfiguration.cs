using CapitalUniversity.Modules.Schedule.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Modules.Schedule.Persistence.Configurations;

public class ScheduleSlotConfiguration : IEntityTypeConfiguration<ScheduleSlot>
{
    public void Configure(EntityTypeBuilder<ScheduleSlot> builder)
    {
        builder.ToTable("ScheduleSlots");

        builder.HasKey(x => x.Id);

        // FK-by-id only — no HasOne to the CourseOffering type. The cross-module
        // modularity rule (see CourseOffering.cs notes) forbids EF navigations
        // across module boundaries. Orphan cleanup when an offering is deleted
        // is the offering's responsibility — its service can call
        // ScheduleSlotRepository.DeleteForOfferingAsync.
        builder.Property(x => x.CourseOfferingId).IsRequired();

        builder.Property(x => x.DayOfWeek).HasConversion<int>().IsRequired();

        // TimeOnly maps natively to SQL Server `time` in EF Core 9 — no
        // converter needed. Stored as wall-clock without a zone so campus
        // timetables read consistently regardless of server locale.
        builder.Property(x => x.StartTime).IsRequired();
        builder.Property(x => x.EndTime).IsRequired();

        builder.Property(x => x.Kind).HasConversion<int>().IsRequired();

        builder.Property(x => x.Location).HasMaxLength(128);
        builder.Property(x => x.Notes).HasMaxLength(512);

        // Hot list path: "give me every slot for this offering, sorted for a
        // weekly view". Day + start time covers the common timetable render.
        builder.HasIndex(x => new { x.CourseOfferingId, x.DayOfWeek, x.StartTime });

        // Uniqueness guard at the DB level — duplicate-row protection only.
        // It does NOT model "no overlapping slots on the same day": detecting
        // overlap is a conflict-engine concern, explicitly out of scope.
        builder.HasIndex(x => new { x.CourseOfferingId, x.DayOfWeek, x.StartTime, x.EndTime })
            .IsUnique();
    }
}
