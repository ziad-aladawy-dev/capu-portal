using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public static class PermissionIdentity
{
    public const string Prefix = "Permission:";

    // CA1861: hoisted so Normalize() doesn't re-allocate the separator array on every call.
    private static readonly char[] NormalizeSeparators = { ' ', '-', '_' };

    public static string Create(string module, string resource, string action)
    {
        return $"{Normalize(module)}.{Normalize(resource)}.{Normalize(action)}";
    }

    public static bool TryParse(string identity, out string module, out string resource, out string action)
    {
        module = string.Empty;
        resource = string.Empty;
        action = string.Empty;

        if (string.IsNullOrWhiteSpace(identity)) return false;

        if (identity.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            identity = identity.Substring(Prefix.Length);
        }

        var parts = identity.Split('.');
        if (parts.Length != 3) return false;

        module = parts[0];
        resource = parts[1];
        action = parts[2];

        return true;
    }

    public static string Parse(string identity)
    {
        if (TryParse(identity, out var module, out var resource, out var action))
        {
            return Create(module, resource, action);
        }

        throw new FormatException($"Invalid permission identity format. Expected Module.Resource.Action, got: {identity}");
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();

        var parts = trimmed.Split(NormalizeSeparators, StringSplitOptions.RemoveEmptyEntries);
        var normalizedParts = parts.Select(p =>
        {
            if (p.Length == 0) return string.Empty;

            bool hasLower = p.Any(char.IsLower);
            bool hasUpper = p.Any(char.IsUpper);

            if (!hasLower || !hasUpper)
            {
                return char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant();
            }
            return char.ToUpperInvariant(p[0]) + p.Substring(1);
        });

        return string.Join("", normalizedParts);
    }
}
