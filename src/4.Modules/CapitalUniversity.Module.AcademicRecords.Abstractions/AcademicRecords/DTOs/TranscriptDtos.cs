namespace CapitalUniversity.Modules.AcademicRecords.Abstractions.DTOs;

/// <summary>
/// One course line in a transcript. Only latest-attempt results appear in a
/// transcript (historical attempts stay in grade history). Non-completed
/// attempts (in-progress / withdrawn / incomplete) surface with
/// <see cref="Grade"/> = <c>"-"</c> per the doc's status rules.
/// </summary>
public class TranscriptCourseDto
{
    public Guid CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseTitle { get; set; } = string.Empty;

    public int CreditHours { get; set; }

    /// <summary>Letter grade, or <c>"-"</c> for any non-completed attempt.</summary>
    public string Grade { get; set; } = "-";

    public decimal? NumericScore { get; set; }

    public AcademicResultStatus Status { get; set; }

    public int CreditsEarned { get; set; }

    /// <summary>True when the course is an elective in the student's academic plan (else compulsory).</summary>
    public bool IsElective { get; set; }
}

/// <summary>
/// A transcript section — one requirement category split into its Compulsory and
/// Elective course lists. The three categories (General, Faculty, Main
/// Specialization) together form a <see cref="TranscriptDto"/>.
/// </summary>
public class TranscriptCategoryDto
{
    public TranscriptRequirementCategory Category { get; set; }

    /// <summary>Localized display label for the category.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public IReadOnlyList<TranscriptCourseDto> Compulsory { get; set; } = new List<TranscriptCourseDto>();

    public IReadOnlyList<TranscriptCourseDto> Elective { get; set; } = new List<TranscriptCourseDto>();
}

/// <summary>
/// Full transcript structure for a student: their synchronized academic summary
/// plus latest-attempt courses grouped into the requirement categories. Built
/// from <c>StudentAcademicResult</c> + Academic Plan + Registered Courses.
/// </summary>
public class TranscriptDto
{
    public Guid StudentId { get; set; }

    /// <summary>Student's display name, resolved from the student record. Empty if the student no longer exists.</summary>
    public string StudentName { get; set; } = string.Empty;

    /// <summary>Student's institutional code (e.g. registration number).</summary>
    public string StudentCode { get; set; } = string.Empty;

    /// <summary>Synchronized summary header (GPA / CGPA / credits / standing). Null if no summary has synced yet.</summary>
    public AcademicSummaryDto? Summary { get; set; }

    /// <summary>The three requirement-category sections, in canonical order.</summary>
    public IReadOnlyList<TranscriptCategoryDto> Categories { get; set; } = new List<TranscriptCategoryDto>();
}

/// <summary>
/// A rendered transcript PDF ready to stream to the client. The bytes are the
/// complete PDF document; <see cref="FileName"/> is a suggested download name.
/// </summary>
public class TranscriptPdfDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public string FileName { get; set; } = "transcript.pdf";

    public string ContentType { get; set; } = "application/pdf";
}
