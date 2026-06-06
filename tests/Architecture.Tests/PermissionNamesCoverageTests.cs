using System.Reflection;
using System.Text.RegularExpressions;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using FluentAssertions;
using Xunit;

namespace CapitalUniversity.Architecture.Tests;

/// <summary>
/// Guards against permission-string drift. Scans every controller source file for
/// <c>[HasPermission("...literal...")]</c> usages and requires each literal to be
/// a value defined on <see cref="PermissionNames"/>. Catches typos and unaudited
/// new attribute usages at build/test time instead of in production.
/// </summary>
public partial class PermissionNamesCoverageTests
{
    [GeneratedRegex("""\[HasPermission\("(?<v>[^"]+)"\)\]""")]
    private static partial Regex HasPermissionLiteralRegex();

    [Fact]
    public void EveryHasPermissionLiteral_MapsToAConstantOnPermissionNames()
    {
        var allConstants = CollectAllPermissionConstants();
        allConstants.Should().NotBeEmpty();

        var sourceRoot = LocateSrcDirectory();
        var offenders = new List<(string File, int Line, string Literal)>();
        var regex = HasPermissionLiteralRegex();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains("\\obj\\") || file.Contains("\\bin\\")) continue;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var match = regex.Match(lines[i]);
                if (!match.Success) continue;
                var literal = match.Groups["v"].Value;
                if (!allConstants.Contains(literal))
                {
                    offenders.Add((file, i + 1, literal));
                }
            }
        }

        offenders.Should().BeEmpty(
            "every [HasPermission(\"...\")] string must be a constant on PermissionNames. " +
            "Offenders: " + string.Join(", ", offenders.Select(o => $"{Path.GetFileName(o.File)}:{o.Line}=\"{o.Literal}\"")));
    }

    private static HashSet<string> CollectAllPermissionConstants()
    {
        var values = new HashSet<string>();
        foreach (var nested in typeof(PermissionNames).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (!field.IsLiteral || field.IsInitOnly) continue;
                if (field.FieldType != typeof(string)) continue;
                var v = (string?)field.GetRawConstantValue();
                if (v != null) values.Add(v);
            }
        }
        return values;
    }

    private static string LocateSrcDirectory()
    {
        // Walk up from the test assembly's bin/ folder until we hit the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "src")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("the test must be run within a tree containing a src/ folder");
        return Path.Combine(dir!.FullName, "src");
    }
}