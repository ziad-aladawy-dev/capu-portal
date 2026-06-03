using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Courses.Push;

public sealed class CourseOutboxValidator : IRecordValidator<CourseOutboxDispatch>
{
    public bool IsValid(CourseOutboxDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Payload.ExternalCourseId))
        {
            error = "Outbox payload missing ExternalCourseId.";
            return false;
        }

        if (!string.Equals(record.Row.ExternalCourseId, record.Payload.ExternalCourseId, StringComparison.Ordinal))
        {
            error = "Outbox payload ExternalCourseId mismatch.";
            return false;
        }

        error = null;
        return true;
    }
}
