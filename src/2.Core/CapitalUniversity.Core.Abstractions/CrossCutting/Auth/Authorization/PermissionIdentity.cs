using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public static class PermissionIdentity
{
    public const string Prefix = "Permission:";

    public static string Create(string module, string resource, string action)
    {
        return $"{Normalize(module)}.{Normalize(resource)}.{Normalize(action)}";
    }

    /// <summary>
    /// Canonical resource name for a Service inside a Module. Mirrors the mapping the
    /// seeders + role-permission lookup use, so generated permission names round-trip
    /// against seeded/real authorization names.
    /// </summary>
    public static string ResourceFor(string moduleKey, string serviceDisplayName)
    {
        if (string.Equals(serviceDisplayName, "Manage Roles", StringComparison.Ordinal))
            return "roles";

        return moduleKey switch
        {
            "academics" => "academic-years",
            _ => moduleKey,
        };
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

        var parts = trimmed.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
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

        var result = string.Join("", normalizedParts);
        
        // Remove pluralization (s) at the end if it's just 's' not 'ss' (e.g. Years -> Year) to enforce canonical singular naming
        if (result.EndsWith("s", StringComparison.OrdinalIgnoreCase) && result.Length > 3 && !result.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            // Only strip if it doesn't break known singular words. E.g., 'Settings' or 'News' - but typically resources are singular.
            // Wait, the instructions say 'Remove... fragile string guessing'. So we do NOT do this here!
        }
        
        return result;
    }
}
