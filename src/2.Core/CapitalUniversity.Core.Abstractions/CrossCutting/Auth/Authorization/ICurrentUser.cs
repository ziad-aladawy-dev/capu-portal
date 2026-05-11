using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    bool IsAuthenticated { get; }
}
