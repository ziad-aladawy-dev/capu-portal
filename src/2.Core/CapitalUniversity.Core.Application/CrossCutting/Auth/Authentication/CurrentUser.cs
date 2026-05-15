using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

namespace CapitalUniversity.Core.Application.Auth.Authentication;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid Id
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idClaim, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public string Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public Guid? StructureNodeId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("StructureNodeId");
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
