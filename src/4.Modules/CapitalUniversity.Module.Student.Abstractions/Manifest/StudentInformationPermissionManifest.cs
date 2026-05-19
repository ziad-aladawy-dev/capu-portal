using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Modules.Student.Abstractions.Manifest;

/// <summary>
/// Declares the Student Information module's permission surface. Profile
/// records are sensitive by default — operators should be granted these
/// narrowly. Verify exists as a separate verb so a clerk can stamp
/// "verified" without being able to modify the underlying data.
/// </summary>
public sealed class StudentInformationPermissionManifest : IPermissionManifest
{
    private const string ResourceProfileRecords = "profile-records";
    private const string DisplayProfileRecords = "Profile Records";

    public string Module => "student-information";
    public string DisplayName => "Student Information";
    public string? Icon => "IdCard";
    public int? OrderNumber => 9;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create(ResourceProfileRecords, "View",      DisplayProfileRecords, 0),
        PermissionDefinition.Create(ResourceProfileRecords, "Insert",    DisplayProfileRecords, 0),
        PermissionDefinition.Create(ResourceProfileRecords, "EditClose", DisplayProfileRecords, 0),
        PermissionDefinition.Create(ResourceProfileRecords, "Open",      DisplayProfileRecords, 0),
        PermissionDefinition.Create(ResourceProfileRecords, "Delete",    DisplayProfileRecords, 0),
    };
}
