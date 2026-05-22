using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using Microsoft.AspNetCore.Http;

namespace CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;

public class ExecutionContext : IExecutionContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly AsyncLocal<bool> SystemMode = new();

    public ExecutionContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var idClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(idClaim, out var id) ? id : Guid.Empty;
        }
    }

    public Guid ActiveRoleId
    {
        get
        {
            var roleClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role);
            return Guid.TryParse(roleClaim, out var roleId) ? roleId : Guid.Empty;
        }
    }

    public string RequestId => _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

    // Default operation if not set explicitly via scope. Behaviors usually determine this.
    public string Operation => _httpContextAccessor.HttpContext?.Request.Path.Value ?? "Unknown";

    public Guid? AuthorizationSourceId
    {
        get
        {
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("AuthZ_SourceId", out var id) == true && id is Guid guidId)
                return guidId;
            return null;
        }
    }

    public SourceType AuthorizationSourceType
    {
        get
        {
            if (_httpContextAccessor.HttpContext?.Items.TryGetValue("AuthZ_SourceType", out var type) == true && type is SourceType sourceType)
                return sourceType;
            return SourceType.None;
        }
    }

    public bool IsSystem => SystemMode.Value;

    public void SetAuthorizationSource(Guid? sourceId, SourceType sourceType)
    {
        if (_httpContextAccessor.HttpContext != null)
        {
            _httpContextAccessor.HttpContext.Items["AuthZ_SourceId"] = sourceId;
            _httpContextAccessor.HttpContext.Items["AuthZ_SourceType"] = sourceType;
        }
    }

    public void SetSystemMode(bool isSystem)
    {
        SystemMode.Value = isSystem;
    }
}
