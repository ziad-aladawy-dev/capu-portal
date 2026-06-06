using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Sources;

/// <summary>
/// Push counterpart to <see cref="IExternalCourseSource"/>. See
/// <c>IExternalStudentSink</c> for the full idempotency-contract narrative.
/// <para>
/// <b>Idempotency contract (REQUIRED).</b> Implementations MUST dedup on
/// <paramref name="idempotencyKey"/> — the outbox writer passes the outbox
/// row's stable <c>Id</c>.
/// </para>
/// </summary>
public interface IExternalCourseSink
{
    Task PushAsync(ExternalCourse payload, string idempotencyKey, CancellationToken cancellationToken);
}
