using FluentAssertions;
using Xunit;

using CoreStaff = CapitalUniversity.Core.Domain.Identity.Staff;
using CoreStudent = CapitalUniversity.Core.Domain.Identity.Student;
using CoreCourse = CapitalUniversity.Core.Domain.Courses.Course;
using StaffValidator = CapitalUniversity.Sync.Staff.Pull.StaffValidator;
using StudentValidator = CapitalUniversity.Sync.Student.Pull.StudentValidator;
using CourseValidator = CapitalUniversity.Sync.Courses.Pull.CourseValidator;
using InvoiceValidator = CapitalUniversity.Sync.Finance.Pull.InvoiceValidator;
using InvoiceSyncDispatch = CapitalUniversity.Sync.Finance.Pull.InvoiceSyncDispatch;
using ScheduleSlotValidator = CapitalUniversity.Sync.Schedules.Pull.ScheduleSlotValidator;
using ScheduleSlotSyncDispatch = CapitalUniversity.Sync.Schedules.Pull.ScheduleSlotSyncDispatch;
using Invoice = CapitalUniversity.Modules.Payments.Domain.Invoice;
using ScheduleSlot = CapitalUniversity.Modules.Schedule.Domain.ScheduleSlot;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Branch-complete coverage for the inbound (Pull) record validators. Each
/// invalid case mutates exactly one field off an otherwise-valid record and
/// asserts the EXACT error string (to kill string/boundary mutations); every
/// validator also has a happy-path test asserting <c>true</c> + null error.
/// </summary>
public class SyncPullValidatorTests
{
    // ---------------- Staff ----------------

    private static CoreStaff ValidStaff() => new()
    {
        ExternallySourced = { ExternalId = "EXT-1" },
        EmployeeCode = "EMP-1",
        Name = "Jane Doe",
        JobTitle = "Lecturer",
        NationalId = "29001011234567",
        BirthDate = new DateTime(1990, 1, 1),
        Email = "jane@uni.edu",
        Role = "Instructor",
    };

    [Fact]
    public void Staff_Valid_ReturnsTrue()
    {
        new StaffValidator().IsValid(ValidStaff(), out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Theory]
    [InlineData("ExternalId", "ExternalId is required (sync merge key).")]
    [InlineData("EmployeeCode", "EmployeeCode is required.")]
    [InlineData("Name", "Name is required.")]
    [InlineData("JobTitle", "JobTitle is required.")]
    [InlineData("NationalId", "NationalId is required.")]
    [InlineData("Email", "Email is required.")]
    [InlineData("Role", "Role is required.")]
    public void Staff_BlankField_Fails(string field, string expected)
    {
        var s = ValidStaff();
        switch (field)
        {
            case "ExternalId": s.ExternallySourced.ExternalId = ""; break;
            case "EmployeeCode": s.EmployeeCode = "  "; break;
            case "Name": s.Name = ""; break;
            case "JobTitle": s.JobTitle = ""; break;
            case "NationalId": s.NationalId = ""; break;
            case "Email": s.Email = ""; break;
            case "Role": s.Role = ""; break;
        }
        new StaffValidator().IsValid(s, out var err).Should().BeFalse();
        err.Should().Be(expected);
    }

    [Fact]
    public void Staff_DefaultBirthDate_Fails()
    {
        var s = ValidStaff(); s.BirthDate = default;
        new StaffValidator().IsValid(s, out var err).Should().BeFalse();
        err.Should().Be("BirthDate is required.");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing@dot")]
    [InlineData("@uni.edu")]
    public void Staff_BadEmail_Fails(string email)
    {
        var s = ValidStaff(); s.Email = email;
        new StaffValidator().IsValid(s, out var err).Should().BeFalse();
        err.Should().Be("Email format invalid.");
    }

    // ---------------- Student ----------------

    private static CoreStudent ValidStudent() => new()
    {
        ExternallySourced = { ExternalId = "EXT-1" },
        StudentCode = "STU-1",
        Name = "Ali Hassan",
        NationalId = "30005061234567",
        BirthDate = new DateTime(2002, 5, 6),
        Email = "ali@students.edu",
    };

    [Fact]
    public void Student_Valid_ReturnsTrue()
    {
        new StudentValidator().IsValid(ValidStudent(), out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Theory]
    [InlineData("ExternalId", "ExternalId is required (sync merge key).")]
    [InlineData("StudentCode", "StudentCode is required.")]
    [InlineData("Name", "Name is required.")]
    [InlineData("NationalId", "NationalId is required.")]
    [InlineData("Email", "Email is required.")]
    public void Student_BlankField_Fails(string field, string expected)
    {
        var s = ValidStudent();
        switch (field)
        {
            case "ExternalId": s.ExternallySourced.ExternalId = ""; break;
            case "StudentCode": s.StudentCode = ""; break;
            case "Name": s.Name = ""; break;
            case "NationalId": s.NationalId = ""; break;
            case "Email": s.Email = ""; break;
        }
        new StudentValidator().IsValid(s, out var err).Should().BeFalse();
        err.Should().Be(expected);
    }

    [Fact]
    public void Student_DefaultBirthDate_Fails()
    {
        var s = ValidStudent(); s.BirthDate = default;
        new StudentValidator().IsValid(s, out var err).Should().BeFalse();
        err.Should().Be("BirthDate is required.");
    }

    [Fact]
    public void Student_BadEmail_Fails()
    {
        var s = ValidStudent(); s.Email = "nope";
        new StudentValidator().IsValid(s, out var err).Should().BeFalse();
        err.Should().Be("Email format invalid.");
    }

    // ---------------- Course ----------------

    private static CoreCourse ValidCourse() => new()
    {
        ExternallySourced = { ExternalId = "EXT-1" },
        Code = "CS101",
        Title = "Intro to CS",
        CreditHours = 3,
    };

    [Fact]
    public void Course_Valid_ReturnsTrue()
    {
        new CourseValidator().IsValid(ValidCourse(), out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void Course_BlankExternalId_Fails()
    {
        var c = ValidCourse(); c.ExternallySourced.ExternalId = "";
        new CourseValidator().IsValid(c, out var err).Should().BeFalse();
        err.Should().Be("ExternalId is required (sync merge key).");
    }

    [Fact]
    public void Course_BlankCode_Fails()
    {
        var c = ValidCourse(); c.Code = "";
        new CourseValidator().IsValid(c, out var err).Should().BeFalse();
        err.Should().Be("Course code is required.");
    }

    [Fact]
    public void Course_BlankTitle_Fails()
    {
        var c = ValidCourse(); c.Title = "";
        new CourseValidator().IsValid(c, out var err).Should().BeFalse();
        err.Should().Be("Course title is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    public void Course_CreditHoursOutOfRange_Fails(int hours)
    {
        var c = ValidCourse(); c.CreditHours = hours;
        new CourseValidator().IsValid(c, out var err).Should().BeFalse();
        err.Should().Be("CreditHours must be in (0, 12].");
    }

    [Fact]
    public void Course_CreditHoursAtMax_ReturnsTrue()
    {
        var c = ValidCourse(); c.CreditHours = 12;
        new CourseValidator().IsValid(c, out _).Should().BeTrue();
    }

    // ---------------- Invoice ----------------

    private static InvoiceSyncDispatch ValidInvoice()
    {
        var inv = new Invoice { TotalAmount = 100m, Currency = "EGP" };
        inv.ExternallySourced.ExternalId = "EXT-1";
        return new InvoiceSyncDispatch { Entity = inv, ExternalStudentId = "STU-EXT-1" };
    }

    [Fact]
    public void Invoice_Valid_ReturnsTrue()
    {
        new InvoiceValidator().IsValid(ValidInvoice(), out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void Invoice_BlankExternalId_Fails()
    {
        var d = ValidInvoice(); d.Entity.ExternallySourced.ExternalId = "";
        new InvoiceValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("ExternalId is required (sync merge key).");
    }

    [Fact]
    public void Invoice_BlankExternalStudentId_Fails()
    {
        var inv = new Invoice { TotalAmount = 100m, Currency = "EGP" };
        inv.ExternallySourced.ExternalId = "EXT-1";
        var d = new InvoiceSyncDispatch { Entity = inv, ExternalStudentId = "" };
        new InvoiceValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("ExternalStudentId is required (every invoice attaches to a student).");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Invoice_NonPositiveTotal_Fails(int total)
    {
        var d = ValidInvoice(); d.Entity.TotalAmount = total;
        new InvoiceValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("TotalAmount must be > 0.");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void Invoice_BadCurrency_Fails(string currency)
    {
        var d = ValidInvoice(); d.Entity.Currency = currency;
        new InvoiceValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("Currency must be a 3-letter ISO 4217 code.");
    }

    // ---------------- ScheduleSlot ----------------

    private static ScheduleSlot ValidSlot()
    {
        var slot = new ScheduleSlot { DayOfWeek = DayOfWeek.Monday };
        slot.ExternallySourced.ExternalId = "EXT-1";
        slot.SetTimeRange(new TimeOnly(8, 0), new TimeOnly(10, 0));
        return slot;
    }

    private static ScheduleSlotSyncDispatch ValidSlotDispatch() =>
        new() { Entity = ValidSlot(), ExternalCourseOfferingId = "OFF-1" };

    [Fact]
    public void ScheduleSlot_Valid_ReturnsTrue()
    {
        new ScheduleSlotValidator().IsValid(ValidSlotDispatch(), out var err).Should().BeTrue();
        err.Should().BeNull();
    }

    [Fact]
    public void ScheduleSlot_BlankExternalId_Fails()
    {
        var d = ValidSlotDispatch(); d.Entity.ExternallySourced.ExternalId = "";
        new ScheduleSlotValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("ExternalId is required (sync merge key).");
    }

    [Fact]
    public void ScheduleSlot_BlankOfferingId_Fails()
    {
        var d = new ScheduleSlotSyncDispatch { Entity = ValidSlot(), ExternalCourseOfferingId = "" };
        new ScheduleSlotValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("ExternalCourseOfferingId is required (every slot attaches to an offering).");
    }

    [Fact]
    public void ScheduleSlot_DayOutOfRange_Fails()
    {
        var slot = new ScheduleSlot { DayOfWeek = (DayOfWeek)7 };
        slot.ExternallySourced.ExternalId = "EXT-1";
        slot.SetTimeRange(new TimeOnly(8, 0), new TimeOnly(10, 0));
        var d = new ScheduleSlotSyncDispatch { Entity = slot, ExternalCourseOfferingId = "OFF-1" };
        new ScheduleSlotValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("DayOfWeek must be in [0, 6] (0 = Sunday … 6 = Saturday).");
    }

    [Fact]
    public void ScheduleSlot_EmptyTimeWindow_Fails()
    {
        // No SetTimeRange call → Start == End == 00:00, so EndTime <= StartTime.
        var slot = new ScheduleSlot { DayOfWeek = DayOfWeek.Monday };
        slot.ExternallySourced.ExternalId = "EXT-1";
        var d = new ScheduleSlotSyncDispatch { Entity = slot, ExternalCourseOfferingId = "OFF-1" };
        new ScheduleSlotValidator().IsValid(d, out var err).Should().BeFalse();
        err.Should().Be("EndTime must be strictly after StartTime.");
    }
}
