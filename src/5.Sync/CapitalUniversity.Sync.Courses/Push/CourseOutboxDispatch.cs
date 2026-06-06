using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Push;

public sealed class CourseOutboxDispatch
{
    public required CourseOutboxEntity Row { get; init; }
    public required ExternalCourse Payload { get; init; }
}
