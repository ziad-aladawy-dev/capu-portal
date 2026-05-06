using System;

namespace CapitalUniversity.Core.Abstractions.Auth.Authorization;

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}
