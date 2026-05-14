using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;

public class RequestContext : IRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue("Id") 
                          ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            
            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string Role => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public Guid? ActiveStructureNodeId => GetGuidFromHeader("X-StructureNode-Id");

    public Guid? ActiveAcademicYearId => GetGuidFromHeader("X-AcademicYear-Id");

    public Guid? ActiveSemesterId => GetGuidFromHeader("X-Semester-Id");

    private Guid? GetGuidFromHeader(string headerName)
    {
        var value = _httpContextAccessor.HttpContext?.Request.Headers[headerName].ToString();
        return Guid.TryParse(value, out var guid) ? guid : null;
    }
}
