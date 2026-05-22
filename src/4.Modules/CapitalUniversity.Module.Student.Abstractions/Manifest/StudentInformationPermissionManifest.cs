using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

namespace CapitalUniversity.Modules.Student.Abstractions.Manifest;

/// <summary>
/// Declares the Student Information module's permission surface. Profile
/// records are sensitive by default — operators should be granted these
/// narrowly.
/// </summary>
public sealed class StudentInformationPermissionManifest : IPermissionManifest
{
    public string Module => "student-information";
    public string DisplayName => "Student Information";
    public string? Icon => "IdCard";
    public int? OrderNumber => 9;

    public IReadOnlyCollection<ResourceDefinition> Resources { get; } = new[]
    {
        ResourceDefinition.WithCrudActions("profile-records", "Profile Records", 0),
    };
}
