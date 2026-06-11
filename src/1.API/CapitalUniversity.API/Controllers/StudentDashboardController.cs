using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications.DTOs;
using CapitalUniversity.Core.Abstractions.Students;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.Registration.Abstractions.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// One-call bootstrap for the student portal dashboard — replaces the fan-out
/// of per-widget requests (student record, academic summary, registered-course
/// count, unread notifications, outstanding fees). Every sub-source is
/// self-scoped to the JWT-bound student id, so the endpoint needs only an
/// authenticated student caller; sources that fail resolve to null/empty so a
/// single sick module never blanks the whole dashboard.
/// </summary>
[ApiController]
[Authorize]
[Route("api/student/dashboard")]
public class StudentDashboardController : ControllerBase
{
    private readonly IStudentService _students;
    private readonly IAcademicRecordsService _academicRecords;
    private readonly IStudentRegistrationService _registrations;
    private readonly INotificationService _notifications;
    private readonly IStudentFeeQueryService _fees;

    public StudentDashboardController(
        IStudentService students,
        IAcademicRecordsService academicRecords,
        IStudentRegistrationService registrations,
        INotificationService notifications,
        IStudentFeeQueryService fees)
    {
        _students = students;
        _academicRecords = academicRecords;
        _registrations = registrations;
        _notifications = notifications;
        _fees = fees;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var studentId = ResolveStudentId();

        var studentTask = Safe(() => _students.GetByIdAsync(studentId));
        var summaryTask = Safe(() => _academicRecords.GetAcademicSummaryAsync(studentId, cancellationToken));
        var registeredTask = Safe(() => _registrations.GetRegisteredCoursesAsync(studentId, cancellationToken));
        var notificationsTask = Safe(() => _notifications.GetUnreadNotificationsAsync(studentId));
        var feesTask = Safe(() => _fees.GetUnpaidForStudentAsync(studentId, cancellationToken));

        await Task.WhenAll(studentTask, summaryTask, registeredTask, notificationsTask, feesTask);

        var student = studentTask.Result;
        if (student is null) return NotFound();

        var registered = registeredTask.Result ?? Array.Empty<RegisteredCourseDto>();
        var unread = (notificationsTask.Result ?? Enumerable.Empty<NotificationDto>()).ToList();
        var unpaidFees = (feesTask.Result ?? Enumerable.Empty<StudentFeeResponse>()).ToList();

        return Ok(new StudentDashboardResponse
        {
            Student = new DashboardStudent
            {
                Id = student.Id,
                Name = student.Name,
                StudentCode = student.StudentCode,
                PhotoUrl = student.PhotoUrl,
                FacultyName = student.FacultyName,
                ProgramName = student.ProgramName,
                LevelName = student.LevelName,
            },
            AcademicSummary = summaryTask.Result,
            RegisteredCourseCount = registered.Count,
            RegisteredCredits = registered.Sum(c => c.CreditHours),
            UnreadNotificationCount = unread.Count,
            LatestNotifications = unread
                .OrderByDescending(n => n.CreatedAt)
                .Take(3)
                .ToList(),
            OutstandingFeeTotal = unpaidFees.Sum(f => f.TotalAmount),
            OutstandingFeeCount = unpaidFees.Count,
        });
    }

    /// <summary>Swallow per-source failures — aggregation degrades, never 500s wholesale.</summary>
    private static async Task<T?> Safe<T>(Func<Task<T?>> action)
    {
        try { return await action(); }
        catch { return default; }
    }

    private Guid ResolveStudentId()
    {
        var id = User.FindFirstValue("Id") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(id, out var guid)) return guid;
        throw new UnauthorizedAccessException();
    }

    public sealed class StudentDashboardResponse
    {
        public DashboardStudent Student { get; set; } = null!;
        public AcademicSummaryDto? AcademicSummary { get; set; }
        public int RegisteredCourseCount { get; set; }
        public int RegisteredCredits { get; set; }
        public int UnreadNotificationCount { get; set; }
        public List<NotificationDto> LatestNotifications { get; set; } = new();
        public decimal OutstandingFeeTotal { get; set; }
        public int OutstandingFeeCount { get; set; }
    }

    public sealed class DashboardStudent
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string? PhotoUrl { get; set; }
        public string FacultyName { get; set; } = string.Empty;
        public string ProgramName { get; set; } = string.Empty;
        public string LevelName { get; set; } = string.Empty;
    }
}
