using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;

namespace CapitalUniversity.Modules.AcademicRecords.Abstractions.Manifest;

/// <summary>
/// Declares the Academic Records module's permission surface. Two resources —
/// the student's grades (history / details / summary) and their transcript
/// (structure + PDF). Read-heavy: in practice only <c>View</c> is exercised by
/// the endpoints, but the canonical CRUD quintet is declared so the permission
/// tree stays uniform with every other module. Row-level access (a student sees
/// only their own records) is enforced in the service layer through
/// <c>IEffectiveScope</c>, not by a coarser action grant.
/// </summary>
public sealed class AcademicRecordsPermissionManifest : IPermissionManifest
{
    public string Module => "academic-records";
    public string DisplayName => LocalizedJson.Of("السجل الأكاديمي", "Academic Records");
    public string? Icon => "GraduationCap";
    public int? OrderNumber => 12;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions(
            "grades",
            LocalizedJson.Of("الدرجات", "Grades"),
            0),
        ResourceDefinition.WithCrudActions(
            "transcript",
            LocalizedJson.Of("السجل الأكاديمي", "Transcript"),
            1),
    };
}
