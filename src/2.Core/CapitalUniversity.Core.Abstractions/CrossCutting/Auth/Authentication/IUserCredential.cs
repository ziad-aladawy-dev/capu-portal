using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IUserCredential
{
    Guid Id { get; }
    string Identifier { get; } // Maps to NationalId
    string PasswordHash { get; }
    DateTime? PasswordExpiry { get; }
    string Role { get; }
    string Name { get; }
    string Email { get; }
    Guid? StructureNodeId { get; }
}
