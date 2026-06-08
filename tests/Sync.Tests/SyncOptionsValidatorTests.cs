using FluentAssertions;
using Xunit;

using StaffOpts = CapitalUniversity.Sync.Staff.Configuration.StaffSyncOptions;
using StaffVal = CapitalUniversity.Sync.Staff.Configuration.StaffSyncOptionsValidator;
using CoursesOpts = CapitalUniversity.Sync.Courses.Configuration.CoursesSyncOptions;
using CoursesVal = CapitalUniversity.Sync.Courses.Configuration.CoursesSyncOptionsValidator;
using SchedulesOpts = CapitalUniversity.Sync.Schedules.Configuration.SchedulesSyncOptions;
using SchedulesVal = CapitalUniversity.Sync.Schedules.Configuration.SchedulesSyncOptionsValidator;
using StudentOpts = CapitalUniversity.Sync.Student.Configuration.StudentSyncOptions;
using StudentVal = CapitalUniversity.Sync.Student.Configuration.StudentSyncOptionsValidator;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Branch-complete coverage for the per-module <c>IValidateOptions</c> guards.
/// Each invalid case asserts the EXACT failure string so string/operator
/// mutations on the diagnostics are killed, and the valid case asserts success.
/// MaxBatchSize is 1000 (SyncLimits.MaxBatchSize) across all modules.
/// </summary>
public class SyncOptionsValidatorTests
{
    private const int Max = 1000; // SyncLimits.MaxBatchSize

    // ---------------- Staff (shared "in (0, Max]" message shape) ----------------

    private static StaffOpts ValidStaff() => new()
    {
        ConnectionString = "Server=.;Database=x;",
        BatchSize = 10,
        PushBatchSize = 10,
        ExtractorSafetyBufferSeconds = 1,
    };

    [Fact]
    public void Staff_Valid_Succeeds()
    {
        var r = new StaffVal().Validate(null, ValidStaff());
        r.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Staff_BlankConnectionString_Fails()
    {
        var o = ValidStaff(); o.ConnectionString = "   ";
        var r = new StaffVal().Validate(null, o);
        r.Failed.Should().BeTrue();
        r.FailureMessage.Should().Be("Sync:Staff:ConnectionString is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(Max + 1)]
    public void Staff_BatchSizeOutOfRange_Fails(int batch)
    {
        var o = ValidStaff(); o.BatchSize = batch;
        var r = new StaffVal().Validate(null, o);
        r.Failed.Should().BeTrue();
        r.FailureMessage.Should().Be($"Sync:Staff:BatchSize must be in (0, {Max}] (was {batch}).");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Max + 1)]
    public void Staff_PushBatchSizeOutOfRange_Fails(int push)
    {
        var o = ValidStaff(); o.PushBatchSize = push;
        var r = new StaffVal().Validate(null, o);
        r.Failed.Should().BeTrue();
        r.FailureMessage.Should().Be($"Sync:Staff:PushBatchSize must be in (0, {Max}] (was {push}).");
    }

    [Fact]
    public void Staff_NegativeBuffer_Fails()
    {
        var o = ValidStaff(); o.ExtractorSafetyBufferSeconds = -1;
        var r = new StaffVal().Validate(null, o);
        r.Failed.Should().BeTrue();
        r.FailureMessage.Should().Be("Sync:Staff:ExtractorSafetyBufferSeconds must be >= 0 (was -1).");
    }

    [Fact]
    public void Staff_BatchSizeAtMax_Succeeds()
    {
        var o = ValidStaff(); o.BatchSize = Max; o.PushBatchSize = Max;
        new StaffVal().Validate(null, o).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Staff_ZeroBuffer_Succeeds()
    {
        var o = ValidStaff(); o.ExtractorSafetyBufferSeconds = 0;
        new StaffVal().Validate(null, o).Succeeded.Should().BeTrue();
    }

    // ---------------- Courses ----------------

    private static CoursesOpts ValidCourses() => new()
    {
        ConnectionString = "Server=.;Database=x;",
        BatchSize = 25,
        PushBatchSize = 25,
        ExtractorSafetyBufferSeconds = 1,
    };

    [Fact]
    public void Courses_Valid_Succeeds() =>
        new CoursesVal().Validate(null, ValidCourses()).Succeeded.Should().BeTrue();

    [Fact]
    public void Courses_BlankConnectionString_Fails()
    {
        var o = ValidCourses(); o.ConnectionString = "";
        new CoursesVal().Validate(null, o).FailureMessage
            .Should().Be("Sync:Courses:ConnectionString is required.");
    }

    [Fact]
    public void Courses_BatchSizeOutOfRange_Fails()
    {
        var o = ValidCourses(); o.BatchSize = 0;
        new CoursesVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Courses:BatchSize must be in (0, {Max}] (was 0).");
    }

    [Fact]
    public void Courses_PushBatchSizeOutOfRange_Fails()
    {
        var o = ValidCourses(); o.PushBatchSize = Max + 1;
        new CoursesVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Courses:PushBatchSize must be in (0, {Max}] (was {Max + 1}).");
    }

    [Fact]
    public void Courses_NegativeBuffer_Fails()
    {
        var o = ValidCourses(); o.ExtractorSafetyBufferSeconds = -2;
        new CoursesVal().Validate(null, o).FailureMessage
            .Should().Be("Sync:Courses:ExtractorSafetyBufferSeconds must be >= 0 (was -2).");
    }

    // ---------------- Schedules ----------------

    private static SchedulesOpts ValidSchedules() => new()
    {
        ConnectionString = "Server=.;Database=x;",
        BatchSize = 25,
        PushBatchSize = 25,
        ExtractorSafetyBufferSeconds = 1,
    };

    [Fact]
    public void Schedules_Valid_Succeeds() =>
        new SchedulesVal().Validate(null, ValidSchedules()).Succeeded.Should().BeTrue();

    [Fact]
    public void Schedules_BlankConnectionString_Fails()
    {
        var o = ValidSchedules(); o.ConnectionString = "";
        new SchedulesVal().Validate(null, o).FailureMessage
            .Should().Be("Sync:Schedules:ConnectionString is required.");
    }

    [Fact]
    public void Schedules_BatchSizeOutOfRange_Fails()
    {
        var o = ValidSchedules(); o.BatchSize = Max + 1;
        new SchedulesVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Schedules:BatchSize must be in (0, {Max}] (was {Max + 1}).");
    }

    [Fact]
    public void Schedules_PushBatchSizeOutOfRange_Fails()
    {
        var o = ValidSchedules(); o.PushBatchSize = -3;
        new SchedulesVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Schedules:PushBatchSize must be in (0, {Max}] (was -3).");
    }

    [Fact]
    public void Schedules_NegativeBuffer_Fails()
    {
        var o = ValidSchedules(); o.ExtractorSafetyBufferSeconds = -10;
        new SchedulesVal().Validate(null, o).FailureMessage
            .Should().Be("Sync:Schedules:ExtractorSafetyBufferSeconds must be >= 0 (was -10).");
    }

    // ---------------- Student (distinct per-branch messages) ----------------

    private static StudentOpts ValidStudent() => new()
    {
        ConnectionString = "Server=.;Database=x;",
        BatchSize = 25,
        PushBatchSize = 25,
        ExtractorSafetyBufferSeconds = 1,
    };

    [Fact]
    public void Student_Valid_Succeeds() =>
        new StudentVal().Validate(null, ValidStudent()).Succeeded.Should().BeTrue();

    [Fact]
    public void Student_BlankConnectionString_Fails()
    {
        var o = ValidStudent(); o.ConnectionString = "   ";
        new StudentVal().Validate(null, o).FailureMessage
            .Should().Be("Sync:Student:ConnectionString is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void Student_BatchSizeNotPositive_Fails(int batch)
    {
        var o = ValidStudent(); o.BatchSize = batch;
        new StudentVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Student:BatchSize must be > 0 (was {batch}).");
    }

    [Fact]
    public void Student_BatchSizeOverMax_Fails()
    {
        var o = ValidStudent(); o.BatchSize = Max + 1;
        new StudentVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Student:BatchSize ({Max + 1}) must be <= {Max} " +
                         "to stay within SQL Server's ~2100 parameter limit per command.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Student_PushBatchSizeNotPositive_Fails(int push)
    {
        var o = ValidStudent(); o.PushBatchSize = push;
        new StudentVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Student:PushBatchSize must be > 0 (was {push}).");
    }

    [Fact]
    public void Student_PushBatchSizeOverMax_Fails()
    {
        var o = ValidStudent(); o.PushBatchSize = Max + 5;
        new StudentVal().Validate(null, o).FailureMessage
            .Should().Be($"Sync:Student:PushBatchSize ({Max + 5}) must be <= {Max}.");
    }

    [Fact]
    public void Student_NegativeBuffer_Fails()
    {
        var o = ValidStudent(); o.ExtractorSafetyBufferSeconds = -1;
        new StudentVal().Validate(null, o).FailureMessage
            .Should().Be("Sync:Student:ExtractorSafetyBufferSeconds must be >= 0 " +
                         "(was -1). Negative values advance the cursor into the future and cause data loss.");
    }

    [Fact]
    public void Student_BatchSizeAtMax_Succeeds()
    {
        var o = ValidStudent(); o.BatchSize = Max; o.PushBatchSize = Max;
        new StudentVal().Validate(null, o).Succeeded.Should().BeTrue();
    }
}
