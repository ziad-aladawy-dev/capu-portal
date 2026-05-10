using System;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization;

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}
