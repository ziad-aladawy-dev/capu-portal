namespace CapitalUniversity.Core.Domain.Courses;

/// <summary>
/// Coarse classification of a <see cref="Course"/>. Drives both UI filtering
/// and academic-plan composition rules. Stored as an int column for index
/// efficiency; the enum values are stable contracts — never reorder.
/// </summary>
public enum CourseCategory
{
    Unspecified = 0,
    ProgramRequirement = 1,
    UniversityRequirement = 2,
    FacultyRequirement = 3,
    Elective = 4,
    GeneralEducation = 5,
}
