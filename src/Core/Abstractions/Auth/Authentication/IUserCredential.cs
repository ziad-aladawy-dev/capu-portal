using System;

namespace CapitalUniversity.Core.Abstractions.Auth.Authentication;

public interface IUserCredential
{
    Guid Id { get; }
    string Identifier { get; } // Maps to NationalId
    string PasswordHash { get; }
    DateTime PasswordExpiry { get; }
    string Role { get; }
    string Name { get; }
    string Email { get; }

    // Minimal attributes to satisfy the UserInfoDto response map
    string UniAttribute { get; }
    string FacultyAttribute { get; }
    string DepartmentAttribute { get; }
}
