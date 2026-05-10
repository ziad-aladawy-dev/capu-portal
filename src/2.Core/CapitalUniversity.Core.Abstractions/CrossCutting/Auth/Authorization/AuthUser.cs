using System;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authorization;

public class AuthUser
{
    public Guid Id { get; set; }
    public string NationalId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
