using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Sources;

public interface IExternalCourseSource
{
    IAsyncEnumerable<ExternalCourse> StreamChangesAsync(
        DateTimeOffset? sinceExclusive,
        CancellationToken cancellationToken);
}
