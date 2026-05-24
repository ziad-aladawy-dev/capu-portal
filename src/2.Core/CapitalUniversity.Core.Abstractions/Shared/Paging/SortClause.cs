using CapitalUniversity.Core.Domain.Common.Exceptions;

namespace CapitalUniversity.Core.Abstractions.Shared.Paging;

/// <summary>
/// One parsed sort directive. The endpoint hands a whitelist of allowed field
/// names to <see cref="Parse"/>; anything outside the whitelist surfaces as a
/// <see cref="ValidationException"/> so a typo never silently falls back to
/// "default sort".
/// </summary>
public sealed class SortClause
{
    public string Field { get; }
    public bool Descending { get; }

    private SortClause(string field, bool descending)
    {
        Field = field;
        Descending = descending;
    }

    /// <summary>
    /// Parse the comma-separated <c>field:asc|desc</c> wire format. Returns an
    /// empty list when <paramref name="raw"/> is null/blank — caller applies
    /// its default ordering. Unknown fields or unknown directions throw
    /// <see cref="ValidationException"/>.
    /// </summary>
    /// <param name="allowedFields">Whitelist — fields the endpoint actually supports sorting by. Comparison is case-insensitive.</param>
    public static IReadOnlyList<SortClause> Parse(string? raw, ISet<string> allowedFields)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<SortClause>();

        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var clauses = new List<SortClause>(parts.Length);

        foreach (var part in parts)
        {
            var colon = part.IndexOf(':');
            string field;
            bool descending;

            if (colon < 0)
            {
                field = part;
                descending = false; // default asc
            }
            else
            {
                field = part[..colon].Trim();
                var dir = part[(colon + 1)..].Trim();
                descending = dir.Equals("desc", StringComparison.OrdinalIgnoreCase);
                if (!descending && !dir.Equals("asc", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException("sort", $"Unknown sort direction '{dir}'. Use 'asc' or 'desc'.");
                }
            }

            if (!allowedFields.Contains(field))
            {
                throw new ValidationException("sort", $"Unknown sort field '{field}'.");
            }
            clauses.Add(new SortClause(field, descending));
        }

        return clauses;
    }
}
