using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// Declares the Student Information module's permission surface. Profile
/// records are sensitive by default — operators should be granted these
/// narrowly. Verify exists as a separate verb so a clerk can stamp
/// "verified" without being able to modify the underlying data.
/// </summary>
public sealed class StudentInformationPermissionManifest : IPermissionManifest
{
    public string Module => "student-information";
    public string DisplayName => "Student Information";
    public string? Icon => "IdCard";
    public int? OrderNumber => 9;

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } = new[]
    {
        PermissionDefinition.Create("profile-records", "View",      "Profile Records", 0),
        PermissionDefinition.Create("profile-records", "Insert",    "Profile Records", 0),
        PermissionDefinition.Create("profile-records", "EditClose", "Profile Records", 0),
        PermissionDefinition.Create("profile-records", "Open",      "Profile Records", 0),
        PermissionDefinition.Create("profile-records", "Delete",    "Profile Records", 0),
    };
}
