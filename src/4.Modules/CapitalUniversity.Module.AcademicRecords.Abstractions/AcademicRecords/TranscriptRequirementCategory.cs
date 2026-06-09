namespace CapitalUniversity.Modules.AcademicRecords.Abstractions;

/// <summary>
/// Top-level grouping a course falls under in a transcript, per
/// <c>docs/Grades/Grades_Model.md</c>. The portal does not own a dedicated
/// "requirement type" field, so this is derived from the existing catalog
/// <c>CourseCategory</c> (reusing the plan/catalog categorization that is
/// available): University/General-Education → <see cref="General"/>, Faculty →
/// <see cref="Faculty"/>, Program → <see cref="MainSpecialization"/>. Within
/// each category the transcript splits Compulsory vs Elective from the academic
/// plan's mandatory flag.
/// </summary>
public enum TranscriptRequirementCategory
{
    /// <summary>General / university-wide requirements (incl. general education).</summary>
    General = 0,

    /// <summary>Faculty-level requirements.</summary>
    Faculty = 1,

    /// <summary>Main specialization (program) requirements.</summary>
    MainSpecialization = 2,
}
