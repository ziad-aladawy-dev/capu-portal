using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    string Role { get; }
    Guid? StructureNodeId { get; }
    bool IsAuthenticated { get; }
}
