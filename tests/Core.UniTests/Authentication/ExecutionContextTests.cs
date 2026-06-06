using System.Security.Claims;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;
using AppExecutionContext = CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication.ExecutionContext;

namespace CapitalUniversity.Core.UniTests.Authentication;

public class ExecutionContextTests
{
    private static (AppExecutionContext ctx, DefaultHttpContext http) Build(ClaimsPrincipal? principal = null)
    {
        var http = new DefaultHttpContext();
        if (principal is not null) http.User = principal;
        var accessor = new HttpContextAccessor { HttpContext = http };
        return (new AppExecutionContext(accessor), http);
    }

    [Fact]
    public void UserId_FromNameIdentifierClaim_ReturnsParsedGuid()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        }));

        var (ctx, _) = Build(principal);

        Assert.Equal(userId, ctx.UserId);
    }

    [Fact]
    public void UserId_NoClaim_ReturnsEmptyGuid()
    {
        var (ctx, _) = Build();
        Assert.Equal(Guid.Empty, ctx.UserId);
    }

    [Fact]
    public void UserId_InvalidClaim_ReturnsEmptyGuid()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid")
        }));
        var (ctx, _) = Build(principal);
        Assert.Equal(Guid.Empty, ctx.UserId);
    }

    [Fact]
    public void ActiveRoleId_FromRoleClaim_ReturnsParsedGuid()
    {
        var roleId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, roleId.ToString())
        }));
        var (ctx, _) = Build(principal);

        Assert.Equal(roleId, ctx.ActiveRoleId);
    }

    [Fact]
    public void ActiveRoleId_NoClaim_ReturnsEmptyGuid()
    {
        var (ctx, _) = Build();
        Assert.Equal(Guid.Empty, ctx.ActiveRoleId);
    }

    [Fact]
    public void RequestId_PrefersHttpTraceIdentifier()
    {
        var (ctx, http) = Build();
        http.TraceIdentifier = "trace-123";
        Assert.Equal("trace-123", ctx.RequestId);
    }

    [Fact]
    public void RequestId_NoHttpContext_FallsBackToNewGuid()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var ctx = new AppExecutionContext(accessor);

        var first = ctx.RequestId;
        Assert.False(string.IsNullOrWhiteSpace(first));
        Assert.True(Guid.TryParse(first, out _));
    }

    [Fact]
    public void Operation_ReturnsRequestPath()
    {
        var (ctx, http) = Build();
        http.Request.Path = "/api/things";

        Assert.Equal("/api/things", ctx.Operation);
    }

    [Fact]
    public void Operation_NoHttpContext_ReturnsUnknown()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var ctx = new AppExecutionContext(accessor);
        Assert.Equal("Unknown", ctx.Operation);
    }

    [Fact]
    public void AuthorizationSource_DefaultsToNullAndNone()
    {
        var (ctx, _) = Build();
        Assert.Null(ctx.AuthorizationSourceId);
        Assert.Equal(SourceType.None, ctx.AuthorizationSourceType);
    }

    [Fact]
    public void SetAuthorizationSource_StoresIdAndType()
    {
        var (ctx, _) = Build();
        var srcId = Guid.NewGuid();

        ctx.SetAuthorizationSource(srcId, SourceType.RoleAssignment);

        Assert.Equal(srcId, ctx.AuthorizationSourceId);
        Assert.Equal(SourceType.RoleAssignment, ctx.AuthorizationSourceType);
    }

    [Fact]
    public void SetAuthorizationSource_NullId_RoundTripsAsNull()
    {
        var (ctx, _) = Build();

        ctx.SetAuthorizationSource(null, SourceType.UserOverride);

        Assert.Null(ctx.AuthorizationSourceId);
        Assert.Equal(SourceType.UserOverride, ctx.AuthorizationSourceType);
    }

    [Fact]
    public void SetAuthorizationSource_NoHttpContext_DoesNotThrow()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var ctx = new AppExecutionContext(accessor);

        ctx.SetAuthorizationSource(Guid.NewGuid(), SourceType.RoleAssignment);

        Assert.Null(ctx.AuthorizationSourceId);
        Assert.Equal(SourceType.None, ctx.AuthorizationSourceType);
    }

    [Fact]
    public void AuthorizationSource_NonMatchingItemTypes_ReturnsDefaults()
    {
        var (ctx, http) = Build();
        http.Items["AuthZ_SourceId"] = "not-a-guid";
        http.Items["AuthZ_SourceType"] = "not-an-enum";

        Assert.Null(ctx.AuthorizationSourceId);
        Assert.Equal(SourceType.None, ctx.AuthorizationSourceType);
    }
}