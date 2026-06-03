using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Sync.Tests;

/// <summary>
/// Source-level contract pin for the sync host's admin endpoint registration
/// pattern. Closes audit finding P1-9 (admin endpoint permission consistency)
/// by failing CI the moment a contributor mounts an <c>/admin/*</c> route
/// outside the policy-protected route group.
///
/// <para>
/// All admin endpoints live inside the single group:
/// <code>
///   var admin = app.MapGroup("/admin").RequireAuthorization(SyncAuthPolicies.SyncAdmin);
///   admin.MapGet("/queues/lag", ...);
///   admin.MapPost("/trigger/{module}", ...);
///   // ...
/// </code>
/// Mounting a route as <c>app.MapGet("/admin/something", ...)</c> directly on
/// the WebApplication skips the policy — the endpoint becomes anonymously
/// reachable when <c>Sync:ExposeAdminEndpoints=true</c>. That's the audit
/// C-1 / P0-2 hole we already closed; this test prevents it from
/// re-emerging through a single line of regress code.
/// </para>
/// </summary>
public class AdminEndpointGroupContractTests
{
    [Fact]
    public void NoAdminRoute_IsMappedOutsideThePolicyProtectedGroup()
    {
        var programPath = LocateProgramCs();
        File.Exists(programPath).Should().BeTrue(
            $"the source-scan needs Sync.Host's Program.cs at the expected location ({programPath})");

        var source = File.ReadAllText(programPath);

        // Strip line comments + block comments before scanning so doc-strings
        // mentioning `app.MapGet("/admin/...")` (which appear in this file's
        // XML doc, for instance) don't trip the check. Order matters:
        // block comments must be removed first because they may span lines
        // that the line-comment regex would otherwise miss.
        var stripped = StripComments(source);

        // Catch any `app.MapGet("/admin/...")` or `app.MapPost("/admin/...")`
        // — i.e., admin routes mounted directly on the WebApplication
        // instead of on the policy-protected group.
        var offenders = Regex.Matches(stripped, @"\bapp\s*\.\s*Map(Get|Post|Put|Patch|Delete)\s*\(\s*""(?<path>/admin/[^""]*)""")
            .Cast<Match>()
            .Select(m => m.Groups["path"].Value)
            .ToList();

        offenders.Should().BeEmpty(
            "every /admin/* route must be mounted on the `admin` route group " +
            "(which applies RequireAuthorization(SyncAuthPolicies.SyncAdmin)). " +
            "Offenders bypass the policy entirely and become anonymously reachable. " +
            "Found: " + string.Join(", ", offenders));
    }

    [Fact]
    public void AdminGroup_AppliesTheSyncAdminPolicy()
    {
        // Complement to the offender-scan above: pin the positive contract —
        // the host wires the group with .RequireAuthorization(SyncAuthPolicies.SyncAdmin).
        // If a refactor renames the policy or swaps in a different one, this
        // test fails loudly rather than silently weakening the gate.
        var programPath = LocateProgramCs();
        var source = File.ReadAllText(programPath);
        var stripped = StripComments(source);

        var hasGroupWiring = Regex.IsMatch(
            stripped,
            @"app\s*\.\s*MapGroup\s*\(\s*""/admin""\s*\)\s*\.\s*RequireAuthorization\s*\(\s*SyncAuthPolicies\s*\.\s*SyncAdmin\s*\)");

        hasGroupWiring.Should().BeTrue(
            "Sync.Host/Program.cs must wire the admin route group as " +
            "`app.MapGroup(\"/admin\").RequireAuthorization(SyncAuthPolicies.SyncAdmin)` " +
            "so every admin endpoint inherits the policy");
    }

    private static string StripComments(string source)
    {
        // Remove /* ... */ block comments (possibly multi-line).
        var noBlock = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        // Remove // line comments — must be after block-stripping so we don't
        // re-process commented-out block comments.
        var noLine = Regex.Replace(noBlock, @"//[^\r\n]*", string.Empty);
        return noLine;
    }

    /// <summary>
    /// Walks up from the test binary directory until it finds the Sync.Host
    /// project's Program.cs. Robust against CI working directories — the
    /// test bin output is typically deep under <c>bin/Debug/net9.0/</c>.
    /// </summary>
    private static string LocateProgramCs()
    {
        // Test binary lives at .../tests/Sync.Tests/bin/Debug/net9.0/CapitalUniversity.Sync.Tests.dll.
        // Walk up to the repo root then descend to the Sync.Host project.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(
                dir,
                "src", "5.Sync", "CapitalUniversity.Sync.Host", "Program.cs");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        // Fall through with the expected path so the assertion message points
        // at the location we tried to find.
        return Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "5.Sync",
            "CapitalUniversity.Sync.Host", "Program.cs");
    }
}
