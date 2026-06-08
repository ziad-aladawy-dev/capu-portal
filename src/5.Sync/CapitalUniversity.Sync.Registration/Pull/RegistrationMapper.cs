using CapitalUniversity.Modules.Registration.Domain;
using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Registration.Domain;

namespace CapitalUniversity.Sync.Registration.Pull;

/// <summary>
/// Maps an upstream <see cref="ExternalRegistration"/> into a Core
/// <see cref="StudentRegisteredCourse"/> wrapped in
/// <see cref="RegistrationSyncDispatch"/>. <c>StudentId</c> / <c>CourseId</c>
/// are left at <see cref="Guid.Empty"/> — <see cref="RegistrationWriter"/>
/// resolves the carried external keys before forwarding to the gateway.
/// Pure + side-effect free, per <see cref="IRecordMapper{TExternal,TInternal}"/>.
/// </summary>
public sealed class RegistrationMapper : IRecordMapper<ExternalRegistration, RegistrationSyncDispatch>
{
    public RegistrationSyncDispatch Map(ExternalRegistration external)
    {
        ArgumentNullException.ThrowIfNull(external);

        var entity = new StudentRegisteredCourse
        {
            ExternallySourced = new()
            {
                ExternalId = external.ExternalRegistrationId.Trim(),
                ExternalUpdatedAt = external.ExternalUpdatedAt.UtcDateTime,
                ExternalVersion = external.ExternalVersion,
            },

            // Resolved by the writer before persistence.
            StudentId = Guid.Empty,
            CourseId = Guid.Empty,

            SemesterId = external.SemesterId,
            StructureNodeId = external.StructureNodeId,
            AttemptNumber = external.AttemptNumber,
            RegistrationStatus = external.Status,
            RegisteredAt = external.RegisteredAt.UtcDateTime,
            CompletedAt = external.CompletedAt?.UtcDateTime,
        };

        return new RegistrationSyncDispatch
        {
            Entity = entity,
            ExternalStudentId = external.ExternalStudentId.Trim(),
            ExternalCourseId = external.ExternalCourseId.Trim(),
        };
    }
}
