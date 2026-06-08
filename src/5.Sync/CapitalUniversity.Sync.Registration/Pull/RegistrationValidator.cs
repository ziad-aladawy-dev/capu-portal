using CapitalUniversity.Sync.Abstractions.Contracts;

namespace CapitalUniversity.Sync.Registration.Pull;

/// <summary>
/// Drops malformed registration dispatches before they reach the writer. Error
/// strings are normalized categories (no per-record data) per
/// <see cref="IRecordValidator{T}"/>, so warning aggregation stays bounded.
/// </summary>
public sealed class RegistrationValidator : IRecordValidator<RegistrationSyncDispatch>
{
    public bool IsValid(RegistrationSyncDispatch record, out string? error)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrWhiteSpace(record.Entity.ExternallySourced.ExternalId))
        {
            error = "ExternalRegistrationId is required (sync merge key).";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.ExternalStudentId))
        {
            error = "ExternalStudentId is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(record.ExternalCourseId))
        {
            error = "ExternalCourseId is required.";
            return false;
        }

        if (record.Entity.SemesterId == Guid.Empty)
        {
            error = "SemesterId is required.";
            return false;
        }

        if (record.Entity.StructureNodeId == Guid.Empty)
        {
            error = "StructureNodeId is required.";
            return false;
        }

        if (record.Entity.AttemptNumber < 1)
        {
            error = "AttemptNumber must be >= 1.";
            return false;
        }

        if (record.Entity.RegisteredAt == default)
        {
            error = "RegisteredAt is required.";
            return false;
        }

        if (record.Entity.CompletedAt is { } completedAt && completedAt < record.Entity.RegisteredAt)
        {
            error = "CompletedAt cannot precede RegisteredAt.";
            return false;
        }

        error = null;
        return true;
    }
}
