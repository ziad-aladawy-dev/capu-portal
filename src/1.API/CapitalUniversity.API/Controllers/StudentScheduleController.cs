using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.StaffManagement;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.Registration.Abstractions.DTOs;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// One-call weekly timetable for the student portal: the caller's ENROLLED
/// registrations → offerings of those courses in the registration's
/// node+semester → schedule slots, flattened to render-ready rows.
///
/// Self-scoped like StudentDashboardController: the student id is bound from
/// the JWT, so the endpoint needs only an authenticated student caller —
/// students hold no CourseOfferings/ScheduleSlots/Courses role grants, which
/// is why the portal cannot use the staff catalog endpoints (they 403).
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/schedule")]
public class StudentScheduleController : ControllerBase
{
    private readonly IStudentRegistrationService _registrations;
    private readonly ICourseOfferingService _offerings;
    private readonly IScheduleSlotService _slots;
    private readonly IStaffService _staff;

    public StudentScheduleController(
        IStudentRegistrationService registrations,
        ICourseOfferingService offerings,
        IScheduleSlotService slots,
        IStaffService staff)
    {
        _registrations = registrations;
        _offerings = offerings;
        _slots = slots;
        _staff = staff;
    }

    /// <param name="semesterId">Optional filter; omitted = all enrolled registrations.</param>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? semesterId, CancellationToken cancellationToken)
    {
        var studentId = ResolveStudentId();

        var registered = await _registrations.GetRegisteredCoursesAsync(studentId, cancellationToken);
        var enrolled = registered
            .Where(r => r.Status == RegistrationStatus.Enrolled)
            .Where(r => semesterId is null || r.SemesterId == semesterId)
            .ToList();
        if (enrolled.Count == 0) return Ok(Array.Empty<StudentScheduleSlotRow>());

        var courseInfo = enrolled
            .GroupBy(r => r.CourseId)
            .ToDictionary(g => g.Key, g => g.First());

        // Resolve offerings per (course, semester). NOT GetForNodeSemesterAsync:
        // registrations carry the student's LEVEL node while offerings are owned
        // by PROGRAM-level nodes, so an exact-node lookup returns nothing. The
        // batch query path-filters rows through EffectiveScope, which lets a
        // student see offerings on any ancestor of their own node.
        var pairs = enrolled
            .Select(r => (r.CourseId, r.SemesterId))
            .Distinct()
            .ToList();
        var allOfferings = await _offerings.GetForCoursesAsync(pairs, cancellationToken);
        var myOfferings = allOfferings
            .Where(o => courseInfo.ContainsKey(o.CourseId))
            .DistinctBy(o => o.Id)
            .ToList();
        if (myOfferings.Count == 0) return Ok(Array.Empty<StudentScheduleSlotRow>());

        // Batch slot query — single SQL round trip.
        var allSlots = await _slots.GetForOfferingsAsync(
            myOfferings.Select(o => o.Id).ToList(),
            cancellationToken);
        var slotsByOffering = allSlots
            .GroupBy(s => s.CourseOfferingId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<StudentScheduleSlotRow>();
        foreach (var offering in myOfferings)
        {
            var course = courseInfo[offering.CourseId];
            if (!slotsByOffering.TryGetValue(offering.Id, out var offeringSlots)) continue;
            foreach (var slot in offeringSlots)
            {
                rows.Add(new StudentScheduleSlotRow
                {
                    Id = slot.Id,
                    CourseOfferingId = offering.Id,
                    DayOfWeek = (int)slot.DayOfWeek, // System.DayOfWeek: 0=Sunday .. 6=Saturday
                    StartTime = slot.StartTime.ToString("HH:mm"),
                    EndTime = slot.EndTime.ToString("HH:mm"),
                    Location = slot.Location,
                    Kind = (int)slot.Kind,
                    IsClosed = slot.IsClosed,
                    CourseId = offering.CourseId,
                    CourseCode = course.CourseCode,
                    CourseTitle = course.CourseTitle,
                    SectionCode = offering.SectionCode,
                    SemesterId = offering.SemesterId,
                });
            }
        }

        // Resolve instructor names from the offerings' InstructorId FK.
        var instructorIds = myOfferings
            .Where(o => o.InstructorId.HasValue)
            .Select(o => o.InstructorId!.Value)
            .Distinct()
            .ToList();
        var instructorMap = new Dictionary<Guid, string>(instructorIds.Count);
        if (instructorIds.Count > 0)
        {
            var staffList = await _staff.GetRangeAsync(instructorIds);
            foreach (var staff in staffList)
            {
                // LocalizedName is "" when the stored name is a plain string
                // (seeded instructors) rather than bilingual JSON — fall back.
                instructorMap[staff.Id] = string.IsNullOrWhiteSpace(staff.LocalizedName)
                    ? staff.Name
                    : staff.LocalizedName;
            }
        }
        var offeringInstructor = myOfferings.ToDictionary(o => o.Id, o => o.InstructorId);
        foreach (var row in rows)
        {
            if (offeringInstructor.TryGetValue(row.CourseOfferingId, out var instrId) && instrId.HasValue)
            {
                instructorMap.TryGetValue(instrId.Value, out var name);
                row.InstructorName = name ?? string.Empty;
            }
        }

        rows.Sort((a, b) => a.DayOfWeek != b.DayOfWeek
            ? a.DayOfWeek.CompareTo(b.DayOfWeek)
            : string.CompareOrdinal(a.StartTime, b.StartTime));
        return Ok(rows);
    }

    private Guid ResolveStudentId()
    {
        var id = User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var guid)) return guid;
        throw new UnauthorizedAccessException();
    }

    public sealed class StudentScheduleSlotRow
    {
        public Guid Id { get; set; }
        public Guid CourseOfferingId { get; set; }
        /// <summary>System.DayOfWeek convention: 0=Sunday .. 6=Saturday.</summary>
        public int DayOfWeek { get; set; }
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string? Location { get; set; }
        public int Kind { get; set; }
        public bool IsClosed { get; set; }
        public Guid CourseId { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public string SectionCode { get; set; } = string.Empty;
        public Guid SemesterId { get; set; }
        public string InstructorName { get; set; } = string.Empty;
    }
}
