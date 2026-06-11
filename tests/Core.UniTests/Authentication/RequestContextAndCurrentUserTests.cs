using System.Security.Claims;
using CapitalUniversity.Core.Application.Auth.Authentication;
using CapitalUniversity.Core.Application.CrossCutting.Auth.Authentication;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CapitalUniversity.Core.UniTests.Authentication;

public class RequestContextAndCurrentUserTests
{
    private static HttpContextAccessor BuildAccessor(
        IEnumerable<Claim>? claims = null,
        IDictionary<string, string>? headers = null,
        bool isAuthenticated = true)
    {
        var http = new DefaultHttpContext();
        if (claims is not null)
        {
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, isAuthenticated ? "TestAuth" : null));
        }
        if (headers is not null)
        {
            foreach (var kvp in headers)
                http.Request.Headers[kvp.Key] = kvp.Value;
        }
        return new HttpContextAccessor { HttpContext = http };
    }

    // -------- RequestContext --------

    [Fact]
    public void RequestContext_PrefersIdClaimOverNameIdentifier()
    {
        var idClaim = Guid.NewGuid();
        var ni = Guid.NewGuid();
        var ctx = new RequestContext(BuildAccessor(new[]
        {
            new Claim("Id", idClaim.ToString()),
            new Claim(ClaimTypes.NameIdentifier, ni.ToString())
        }));

        Assert.Equal(idClaim, ctx.UserId);
    }

    [Fact]
    public void RequestContext_FallsBackToNameIdentifier()
    {
        var ni = Guid.NewGuid();
        var ctx = new RequestContext(BuildAccessor(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ni.ToString())
        }));

        Assert.Equal(ni, ctx.UserId);
    }

    [Fact]
    public void RequestContext_NoClaim_UserIdNull()
    {
        var ctx = new RequestContext(BuildAccessor());
        Assert.Null(ctx.UserId);
    }

    [Fact]
    public void RequestContext_RoleClaim_RoundTripsAsString()
    {
        var ctx = new RequestContext(BuildAccessor(new[] { new Claim(ClaimTypes.Role, "Admin") }));
        Assert.Equal("Admin", ctx.Role);
    }

    [Fact]
    public void RequestContext_MissingRole_ReturnsEmpty()
    {
        var ctx = new RequestContext(BuildAccessor());
        Assert.Equal(string.Empty, ctx.Role);
    }

    [Fact]
    public void RequestContext_ParsesActiveContextHeaders()
    {
        var structureId = Guid.NewGuid();
        var yearId = Guid.NewGuid();
        var semId = Guid.NewGuid();
        var ctx = new RequestContext(BuildAccessor(headers: new Dictionary<string, string>
        {
            ["X-StructureNode-Id"] = structureId.ToString(),
            ["X-AcademicYear-Id"]  = yearId.ToString(),
            ["X-Semester-Id"]      = semId.ToString(),
        }));

        Assert.Equal(structureId, ctx.ActiveStructureNodeId);
        Assert.Equal(yearId,      ctx.ActiveAcademicYearId);
        Assert.Equal(semId,       ctx.ActiveSemesterId);
    }

    [Fact]
    public void RequestContext_InvalidHeader_ReturnsNull()
    {
        var ctx = new RequestContext(BuildAccessor(headers: new Dictionary<string, string>
        {
            ["X-StructureNode-Id"] = "nope"
        }));
        Assert.Null(ctx.ActiveStructureNodeId);
    }

    // -------- CurrentUser --------

    [Fact]
    public void CurrentUser_Id_FromNameIdentifier()
    {
        var uid = Guid.NewGuid();
        var u = new CurrentUser(BuildAccessor(new[] { new Claim(ClaimTypes.NameIdentifier, uid.ToString()) }));
        Assert.Equal(uid, u.Id);
    }

    [Fact]
    public void CurrentUser_Id_NoClaim_ReturnsEmpty()
    {
        var u = new CurrentUser(BuildAccessor());
        Assert.Equal(Guid.Empty, u.Id);
    }

    // M4 — CurrentUser.Email removed: PII no longer travels in the JWT and the
    // property had no consumers. (Test for it deleted along with the property.)

    [Fact]
    public void CurrentUser_Role_FromClaim()
    {
        var u = new CurrentUser(BuildAccessor(new[] { new Claim(ClaimTypes.Role, "Auditor") }));
        Assert.Equal("Auditor", u.Role);
    }

    [Fact]
    public void CurrentUser_StructureNodeId_ParsedFromCustomClaim()
    {
        var nodeId = Guid.NewGuid();
        var u = new CurrentUser(BuildAccessor(new[] { new Claim("StructureNodeId", nodeId.ToString()) }));
        Assert.Equal(nodeId, u.StructureNodeId);
    }

    [Fact]
    public void CurrentUser_StructureNodeId_MissingClaim_ReturnsNull()
    {
        var u = new CurrentUser(BuildAccessor());
        Assert.Null(u.StructureNodeId);
    }

    [Fact]
    public void CurrentUser_IsAuthenticated_TracksIdentity()
    {
        var withAuth = new CurrentUser(BuildAccessor(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, isAuthenticated: true));
        Assert.True(withAuth.IsAuthenticated);

        var noAuth = new CurrentUser(BuildAccessor());
        Assert.False(noAuth.IsAuthenticated);
    }
}