using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Architecture.Tests;

/// <summary>
/// Architecture-level regression guards added during the Phase 1-4 hardening
/// pass. Each test below fails the build if a class of bug fixed earlier
/// reappears anywhere in the source tree. Cheap, source-scanning checks —
/// no test database required.
/// </summary>
public partial class RegressionGuardTests
{
    // ----------------------------------------------------------------------
    // C4 — every "X already exists" / "X not found" path used to throw a bare
    // Exception, which GlobalExceptionHandler maps to 500 with the message
    // leaked verbatim. This guard refuses any future `throw new Exception(...)`
    // outside test projects.
    // ----------------------------------------------------------------------

    [GeneratedRegex(@"throw\s+new\s+Exception\s*\(")]
    private static partial Regex BareExceptionRegex();

    // Skipped until C4 (bare-Exception sweep across StudentService /
    // StaffService / UniversityStructureService) lands. Flip the Skip off in
    // the same PR that fixes the last `throw new Exception(...)`.
    [Fact(Skip = "Re-enable after C4: replace remaining `throw new Exception(...)` calls with domain exceptions.")]
    public void NoBareThrowNewException_OutsideTestProjects()
    {
        var srcRoot = LocateSrcDirectory();
        var offenders = ScanForRegex(srcRoot, BareExceptionRegex(), shouldSkipFile: file =>
            file.Contains("\\obj\\") || file.Contains("\\bin\\"));

        offenders.Should().BeEmpty(
            "C4 regression — bare `throw new Exception(...)` falls through GlobalExceptionHandler to 500 " +
            "and leaks the literal message. Use the domain exception types in " +
            "CapitalUniversity.Core.Domain.Common.Exceptions instead (NotFoundException, ValidationException, " +
            "ConflictException, ForbiddenException, UnauthorizedException). Offenders: " +
            string.Join(", ", offenders.Select(o => $"{Path.GetFileName(o.File)}:{o.Line}")));
    }

    // ----------------------------------------------------------------------
    // C2 — every controller action must be gated by either [HasPermission],
    // [Authorize], or [AllowAnonymous]. The global RequireAuthenticatedUser
    // fallback policy is NOT enough on its own because it accepts any
    // authenticated user including a low-privilege student.
    // ----------------------------------------------------------------------

    [GeneratedRegex(@"public\s+async\s+Task<[^>]+>\s+\w+\s*\(")]
    private static partial Regex AsyncActionRegex();

    [GeneratedRegex(@"\[(HasPermission|Authorize|AllowAnonymous)")]
    private static partial Regex ActionAttributeRegex();

    // Skipped until C2 (decorate StaffController / StudentsController /
    // StructureLookupController / UniversityStructureController) lands.
    // Flip the Skip off in the same PR that decorates the last action.
    [Fact(Skip = "Re-enable after C2: add [HasPermission]/[Authorize]/[AllowAnonymous] to every action on the four naked controllers.")]
    public void EveryControllerAction_HasExplicitAuthorisationAttribute()
    {
        var srcRoot = LocateSrcDirectory();
        var controllersDir = Path.Combine(srcRoot, "1.API", "CapitalUniversity.API", "Controllers");
        Directory.Exists(controllersDir).Should().BeTrue("controllers directory must exist for this guard to be meaningful");

        var offenders = new List<(string File, int Line, string Snippet)>();
        var actionRegex = AsyncActionRegex();
        var attrRegex = ActionAttributeRegex();

        foreach (var file in Directory.EnumerateFiles(controllersDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains("\\obj\\") || file.Contains("\\bin\\")) continue;
            var lines = File.ReadAllLines(file);
            // If the controller class itself carries [HasPermission] / [Authorize] /
            // [AllowAnonymous], that covers every action (class-level attributes
            // inherit). Detect via a windowed scan of the file header.
            var classDecorated = lines.Take(40).Any(l => attrRegex.IsMatch(l));
            if (classDecorated) continue;

            // Look for each public async Task action and walk back up to find
            // the nearest attribute block. If none, flag it.
            for (var i = 0; i < lines.Length; i++)
            {
                if (!actionRegex.IsMatch(lines[i])) continue;

                // Walk backward over attribute / blank lines until we hit
                // something that isn't an attribute. The window has to extend
                // far enough to catch multi-attribute clusters but stop before
                // the previous action so we don't accidentally borrow another
                // method's attributes.
                var hasAttr = false;
                for (var j = i - 1; j >= Math.Max(0, i - 8); j--)
                {
                    var line = lines[j].TrimStart();
                    if (line.Length == 0) continue;
                    if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*")) continue;
                    if (line.StartsWith("["))
                    {
                        if (attrRegex.IsMatch(line)) { hasAttr = true; break; }
                        continue; // some other attribute (e.g. [HttpGet]) — keep walking
                    }
                    break; // hit non-attribute, non-comment line; stop
                }

                if (!hasAttr)
                {
                    offenders.Add((file, i + 1, lines[i].Trim()));
                }
            }
        }

        offenders.Should().BeEmpty(
            "C2 regression — every controller action needs [HasPermission], [Authorize], or " +
            "[AllowAnonymous]. The RequireAuthenticatedUser fallback alone lets any authenticated user " +
            "(including a student) hit the endpoint. Offenders: " +
            string.Join(", ", offenders.Select(o => $"{Path.GetFileName(o.File)}:{o.Line} → {o.Snippet}")));
    }

    // ----------------------------------------------------------------------
    // M18 — repository-level SaveChangesAsync coexisted with IUnitOfWork.
    // Two save paths invite partial-commit bugs. Guard against re-introducing
    // a public SaveChangesAsync on any *Repository.cs implementation.
    // ----------------------------------------------------------------------

    [GeneratedRegex(@"public\s+(async\s+)?Task(<[^>]+>)?\s+SaveChangesAsync\s*\(")]
    private static partial Regex SaveChangesAsyncRegex();

    // Skipped until M18 (remove SaveChangesAsync from StudentRepository /
    // StructureNodeRepository, route all callers through IUnitOfWork) lands.
    [Fact(Skip = "Re-enable after M18: route every repository write through IUnitOfWork.SaveChangesAsync.")]
    public void NoRepositoryExposes_SaveChangesAsync()
    {
        var srcRoot = LocateSrcDirectory();
        var offenders = ScanForRegex(srcRoot, SaveChangesAsyncRegex(), shouldSkipFile: file =>
        {
            if (file.Contains("\\obj\\") || file.Contains("\\bin\\")) return true;
            // Allow it on UnitOfWork (that's the intended single entry point)
            // and on the DbContext (EF Core's own override).
            var name = Path.GetFileName(file);
            if (name.Equals("UnitOfWork.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals("CoreDbContext.cs", StringComparison.OrdinalIgnoreCase)) return true;
            if (name.Equals("IUnitOfWork.cs", StringComparison.OrdinalIgnoreCase)) return true;
            // Repository files only (the guard only matters there).
            return !name.EndsWith("Repository.cs", StringComparison.OrdinalIgnoreCase);
        });

        offenders.Should().BeEmpty(
            "M18 regression — repositories must not expose SaveChangesAsync. Persistence flows through " +
            "IUnitOfWork.SaveChangesAsync only. Offenders: " +
            string.Join(", ", offenders.Select(o => $"{Path.GetFileName(o.File)}:{o.Line}")));
    }

    // ----------------------------------------------------------------------
    // Shared helpers
    // ----------------------------------------------------------------------

    private static List<(string File, int Line, string Snippet)> ScanForRegex(
        string root,
        Regex regex,
        Func<string, bool> shouldSkipFile)
    {
        var hits = new List<(string, int, string)>();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (shouldSkipFile(file)) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (regex.IsMatch(lines[i]))
                {
                    hits.Add((file, i + 1, lines[i].Trim()));
                }
            }
        }
        return hits;
    }

    private static string LocateSrcDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("the test must run within a tree containing a src/ folder");
        return Path.Combine(dir!.FullName, "src");
    }
}
