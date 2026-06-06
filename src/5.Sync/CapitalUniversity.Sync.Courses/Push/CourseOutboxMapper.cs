using System.Text.Json;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Courses.Domain;

namespace CapitalUniversity.Sync.Courses.Push;

public sealed class CourseOutboxMapper : IRecordMapper<CourseOutboxEntity, CourseOutboxDispatch>
{
    public CourseOutboxDispatch Map(CourseOutboxEntity external)
    {
        ArgumentNullException.ThrowIfNull(external);

        if (external.PayloadSchemaVersion != CourseOutboxEntity.CurrentPayloadSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Outbox payload schema version mismatch for ExternalCourseId={external.ExternalCourseId}: " +
                $"row={external.PayloadSchemaVersion} expected={CourseOutboxEntity.CurrentPayloadSchemaVersion}. " +
                "Migrate the row or extend the mapper before retrying.");
        }

        ExternalCourse payload;
        try
        {
            payload = OutboxPayloadSerializer.Deserialize<ExternalCourse>(external.Payload);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Outbox payload JSON invalid for ExternalCourseId={external.ExternalCourseId}: {ex.Message}",
                ex);
        }

        return new CourseOutboxDispatch
        {
            Row = external,
            Payload = payload
        };
    }
}
