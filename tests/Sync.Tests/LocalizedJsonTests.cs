using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Sync.Abstractions.Localization;
using CapitalUniversity.Sync.Courses.Domain;
using CapitalUniversity.Sync.Courses.Pull;
using CapitalUniversity.Sync.Finance.Domain;
using CapitalUniversity.Sync.Finance.Pull;
using CapitalUniversity.Sync.Schedules.Domain;
using CapitalUniversity.Sync.Schedules.Pull;
using CapitalUniversity.Sync.Staff.Domain;
using CapitalUniversity.Sync.Staff.Pull;
using CapitalUniversity.Sync.Student.Domain;
using CapitalUniversity.Sync.Student.Pull;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Two-part coverage for the localization wiring:
///   (a) <see cref="LocalizedJson"/> shape contract — Normalize coerces every
///       input form into the canonical <c>{"ar":"…","en":"…"}</c> object;
///       IsEmpty matches null / whitespace / blank-bilingual JSON; round-trip
///       preserves real bilingual values.
///   (b) Each module's Pull mapper actually emits bilingual JSON for the
///       display-text columns and leaves technical identifiers raw, matching
///       the Core domain's column shape.
/// </summary>
public class LocalizedJsonTests
{
    // ── (a) LocalizedJson shape contract ─────────────────────────────────────

    [Fact]
    public void Normalize_PlainText_EchoesIntoBothCultures()
    {
        var json = LocalizedJson.Normalize("Mathematics");
        json.Should().Be("{\"ar\":\"Mathematics\",\"en\":\"Mathematics\"}");
    }

    [Fact]
    public void Normalize_NullOrWhitespace_ProducesEmptyBilingual()
    {
        LocalizedJson.Normalize(null).Should().Be("{\"ar\":\"\",\"en\":\"\"}");
        LocalizedJson.Normalize(string.Empty).Should().Be("{\"ar\":\"\",\"en\":\"\"}");
        LocalizedJson.Normalize("   ").Should().Be("{\"ar\":\"\",\"en\":\"\"}");
    }

    [Fact]
    public void Normalize_AlreadyBilingual_ReturnsCanonicalShape()
    {
        var json = LocalizedJson.Normalize("{\"ar\":\"رياضيات\",\"en\":\"Mathematics\"}");
        json.Should().Be("{\"ar\":\"رياضيات\",\"en\":\"Mathematics\"}");
    }

    [Fact]
    public void Normalize_HalfLocalized_FillsMissingCultureWithBlank()
    {
        LocalizedJson.Normalize("{\"en\":\"Physics\"}")
            .Should().Be("{\"ar\":\"\",\"en\":\"Physics\"}");
        LocalizedJson.Normalize("{\"ar\":\"فيزياء\"}")
            .Should().Be("{\"ar\":\"فيزياء\",\"en\":\"\"}");
    }

    [Fact]
    public void Normalize_EscapesQuotesAndBackslashes()
    {
        var json = LocalizedJson.Normalize("path\\to\\\"thing\"");
        json.Should().Be("{\"ar\":\"path\\\\to\\\\\\\"thing\\\"\",\"en\":\"path\\\\to\\\\\\\"thing\\\"\"}");
    }

    [Fact]
    public void Extract_ReturnsCultureValue_OrFallsBackToInput()
    {
        var json = LocalizedJson.Of("رياضيات", "Mathematics");
        LocalizedJson.Extract(json, "en").Should().Be("Mathematics");
        LocalizedJson.Extract(json, "ar").Should().Be("رياضيات");
        LocalizedJson.Extract(json, "fr").Should().Be(json, "missing culture falls back to the original blob");

        LocalizedJson.Extract("legacy", "en").Should().Be("legacy");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("{\"ar\":\"\",\"en\":\"\"}", true)]
    [InlineData("{\"ar\":\"  \",\"en\":\"\"}", true)]
    [InlineData("{\"ar\":\"\"}", true)]
    [InlineData("legacy plain text", false)]
    [InlineData("{\"ar\":\"\",\"en\":\"Mathematics\"}", false)]
    [InlineData("{\"ar\":\"رياضيات\",\"en\":\"Mathematics\"}", false)]
    public void IsEmpty_RecognizesBlankBilingual_AndPlainText(string? input, bool expected)
    {
        LocalizedJson.IsEmpty(input).Should().Be(expected);
    }

    // ── (b) Per-module mappers → Core entities ───────────────────────────────

    [Fact]
    public void StudentMapper_LocalizesName_KeepsIdentifiersRaw()
    {
        // Mapper produces a Core Identity.Student directly. ExternalId is the
        // sync merge key (used by ICoreWriteGateway).
        var entity = new StudentMapper().Map(new ExternalStudent
        {
            ExternalStudentId = "EXT-S-0001",
            StudentCode = "STU-1001",
            Name = "John Doe",
            NationalId = "NID-12345",
            BirthDate = new DateTime(2001, 5, 12),
            PhoneNumber = "+201234567890",
            Email = "John.Doe@university.test",
            IsActive = true,
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        });

        entity.ExternallySourced.ExternalId.Should().Be("EXT-S-0001", "the gateway merges by ExternallySourced.ExternalId");
        entity.Name.Should().Be("{\"ar\":\"John Doe\",\"en\":\"John Doe\"}");
        entity.StudentCode.Should().Be("STU-1001");
        entity.NationalId.Should().Be("NID-12345");
        entity.PhoneNumber.Should().Be("+201234567890");
        entity.BirthDate.Should().Be(new DateTime(2001, 5, 12));
        entity.Email.Should().Be("john.doe@university.test", "Email lower-cased for case-insensitive equality");
        entity.IsActive.Should().BeTrue();
    }

    [Fact]
    public void StaffMapper_LocalizesNameAndJobTitle_KeepsIdentifiersRaw()
    {
        // Mapper produces a Core Identity.Staff. Name + JobTitle are bilingual
        // (matches StaffService.CreateAsync); Role is the application-defined
        // role string and stays raw.
        var entity = new StaffMapper().Map(new ExternalStaff
        {
            ExternalStaffId = "EXT-T-0001",
            EmployeeCode = "EMP-2001",
            Name = "Ada Lovelace",
            NationalId = "NID-T-1",
            BirthDate = new DateTime(1980, 1, 1),
            PhoneNumber = "+201234567890",
            Email = "Ada@university.test",
            Role = "instructor",
            JobTitle = "Lecturer",
            IsActive = true,
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        });

        entity.ExternallySourced.ExternalId.Should().Be("EXT-T-0001");
        entity.Name.Should().Be("{\"ar\":\"Ada Lovelace\",\"en\":\"Ada Lovelace\"}");
        entity.JobTitle.Should().Be("{\"ar\":\"Lecturer\",\"en\":\"Lecturer\"}");
        entity.Role.Should().Be("instructor", "system roles are application identifiers, not display text");
        entity.EmployeeCode.Should().Be("EMP-2001");
        entity.Email.Should().Be("ada@university.test");
    }

    [Fact]
    public void CourseMapper_LocalizesCodeAndTitle_KeepsCreditHoursAndCategoryRaw()
    {
        // Mapper produces a Core Courses.Course. Code + Title are bilingual
        // (matches Core CourseMapper.ForceNormalizeIncoming).
        var entity = new CourseMapper().Map(new ExternalCourse
        {
            ExternalCourseId = "EXT-C-0001",
            Code = "MATH-101",
            Title = "Intro to Mathematics",
            CreditHours = 3,
            Category = CourseCategory.ProgramRequirement,
            IsActive = true,
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        });

        entity.ExternallySourced.ExternalId.Should().Be("EXT-C-0001");
        // Core's Course.Code setter uppercases the value — the JSON literal flows
        // through unchanged structurally; the test pins the bilingual shape.
        entity.Code.Should().Be("{\"AR\":\"MATH-101\",\"EN\":\"MATH-101\"}");
        entity.Title.Should().Be("{\"ar\":\"Intro to Mathematics\",\"en\":\"Intro to Mathematics\"}");
        entity.CreditHours.Should().Be(3);
        entity.Category.Should().Be(CourseCategory.ProgramRequirement);
        entity.IsActive.Should().BeTrue();
    }

    [Fact]
    public void InvoiceMapper_ProducesDispatchWithRawInvoice_AndUpstreamStudentKey()
    {
        // Invoice headers carry no localized text. The mapper returns a
        // InvoiceSyncDispatch wrapper: the Core Invoice entity (with
        // StudentId left at Guid.Empty for the writer to resolve) plus the
        // upstream student key the writer feeds into ICoreWriteGateway.ResolveIdByExternalIdAsync.
        var dispatch = new InvoiceMapper().Map(new ExternalInvoice
        {
            ExternalInvoiceId = "EXT-INV-000001",
            ExternalStudentId = "EXT-S-0001",
            Status = InvoiceStatus.PartiallyPaid,
            TotalAmount = 1234.56m,
            Currency = "egp",
            DueAt = new DateTime(2026, 12, 31),
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        });

        // Wrapper carries the upstream student key so the writer can resolve it.
        dispatch.ExternalStudentId.Should().Be("EXT-S-0001");

        // Entity is the Core Invoice the gateway will upsert.
        dispatch.Entity.ExternallySourced.ExternalId.Should().Be("EXT-INV-000001");
        dispatch.Entity.StudentId.Should().Be(Guid.Empty, "writer rebinds this after gateway resolves the upstream key");
        dispatch.Entity.Status.Should().Be(InvoiceStatus.PartiallyPaid);
        dispatch.Entity.TotalAmount.Should().Be(1234.56m);
        dispatch.Entity.Currency.Should().Be("EGP", "ISO codes stay raw + uppercase");
        dispatch.Entity.DueAt.Should().Be(new DateTime(2026, 12, 31));
    }

    [Fact]
    public void ScheduleSlotMapper_LocalizesLocationAndNotes_KeepsTimesRaw()
    {
        // Mapper returns ScheduleSlotSyncDispatch — the Core ScheduleSlot with
        // CourseOfferingId left at Guid.Empty (writer resolves), plus the
        // upstream offering key on the wrapper. SetTimeRange enforces End > Start
        // at construction.
        var dispatch = new ScheduleSlotMapper().Map(new ExternalScheduleSlot
        {
            ExternalScheduleSlotId = "EXT-SCH-00001",
            ExternalCourseOfferingId = "EXT-CO-0001",
            DayOfWeek = DayOfWeek.Wednesday,
            StartTime = new TimeOnly(9, 0),
            EndTime = new TimeOnly(10, 30),
            Kind = ScheduleSlotKind.Lecture,
            Location = "Room A-101",
            Notes = "Bring laptop",
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        });

        dispatch.ExternalCourseOfferingId.Should().Be("EXT-CO-0001");

        dispatch.Entity.ExternallySourced.ExternalId.Should().Be("EXT-SCH-00001");
        dispatch.Entity.CourseOfferingId.Should().Be(Guid.Empty, "writer rebinds after gateway FK resolution");
        dispatch.Entity.Location.Should().Be("{\"ar\":\"Room A-101\",\"en\":\"Room A-101\"}");
        dispatch.Entity.Notes.Should().Be("{\"ar\":\"Bring laptop\",\"en\":\"Bring laptop\"}");
        dispatch.Entity.StartTime.Should().Be(new TimeOnly(9, 0));
        dispatch.Entity.EndTime.Should().Be(new TimeOnly(10, 30));
        dispatch.Entity.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        dispatch.Entity.Kind.Should().Be(ScheduleSlotKind.Lecture);
    }

    [Fact]
    public void ScheduleSlotMapper_NullLocationAndNotes_StayNull()
    {
        // Location / Notes are optional. null-in-null-out so the column
        // distinguishes "upstream didn't supply" from "empty in both cultures".
        var dispatch = new ScheduleSlotMapper().Map(new ExternalScheduleSlot
        {
            ExternalScheduleSlotId = "EXT-SCH-00002",
            ExternalCourseOfferingId = "EXT-CO-0001",
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(11, 0),
            Kind = ScheduleSlotKind.Lab,
            Location = null,
            Notes = null,
            ExternalUpdatedAt = DateTimeOffset.UtcNow,
            ExternalVersion = 1
        });

        dispatch.Entity.Location.Should().BeNull("null upstream Location must not become {\"ar\":\"\",\"en\":\"\"}");
        dispatch.Entity.Notes.Should().BeNull();
    }
}
