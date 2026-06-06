namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// A resource declared by a module — e.g. <c>"invoices"</c> inside the
/// <c>payments</c> module. Carries the stable slug (<see cref="Key"/>) used in
/// the canonical <c>{module}.{resource}.{action}</c> permission identity, the
/// display name surfaced to admin UI, and the set of action verbs this resource
/// supports. The synchroniser materialises one <c>Resource</c> row per
/// <c>(ModuleId, Key)</c>; the <c>(Key, Action)</c> tuple is unique inside a
/// manifest.
/// </summary>
public sealed record ResourceDefinition
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int OrderNumber { get; init; }
    public IReadOnlyCollection<ActionDefinition> Actions { get; init; } = Array.Empty<ActionDefinition>();

    // M13 — Previous key(s) this resource was known under. Used by the
    // synchroniser to migrate existing grants (RolePermission,
    // StaffPermissionOverride) onto the new key inside a single transaction
    // when a manifest rename ships. Null/empty = no rename happening; the
    // synchroniser treats the row as a normal upsert.
    //
    // Callers MUST drop a previous key from this list once the corresponding
    // production migration has run, otherwise re-running the synchroniser on
    // a freshly-renamed key picks up legacy keys it should no longer touch.
    public IReadOnlyCollection<string>? PreviousKeys { get; init; }

    /// <summary>
    /// Declares a resource where actions are listed verbatim with no inheritance
    /// relationships. Used by tiny resources (e.g. notifications with View+Insert)
    /// or non-CRUD resources.
    /// </summary>
    public static ResourceDefinition Of(string key, string displayName, int orderNumber, params string[] actions) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            OrderNumber = orderNumber,
            Actions = actions.Select((a, i) => ActionDefinition.Of(a, i)).ToList(),
        };

    /// <summary>
    /// Shorthand for the canonical CRUD-ish quintet used by the majority of
    /// admin-style resources: <c>View, Insert, EditClose, Open, Delete</c>.
    /// Inheritance is declared explicitly per <see cref="ActionDefinition.Implies"/>
    /// — each higher verb implies every verb below it.
    /// </summary>
    public static ResourceDefinition WithCrudActions(string key, string displayName, int orderNumber = 0) =>
        new()
        {
            Key = key,
            DisplayName = displayName,
            OrderNumber = orderNumber,
            Actions = new[]
            {
                ActionDefinition.Hierarchical("View",      0),
                ActionDefinition.Hierarchical("Insert",    1, "View"),
                ActionDefinition.Hierarchical("EditClose", 2, "View", "Insert"),
                ActionDefinition.Hierarchical("Open",      3, "View", "Insert", "EditClose"),
                ActionDefinition.Hierarchical("Delete",    4, "View", "Insert", "EditClose", "Open"),
            },
        };

    public string CanonicalName(string module, string action) => $"{module}.{Key}.{action}";

    /// <summary>
    /// Returns the closure of an action under this resource's <see cref="ActionDefinition.Implies"/>
    /// — the action itself plus every transitively-implied action. Unknown action
    /// names yield an empty set; cycles are broken on first revisit.
    /// <para>Used by <b>allow</b> grants: holding <c>Delete</c> means holding everything
    /// <c>Delete</c> implies.</para>
    /// </summary>
    public IReadOnlySet<string> ExpandImplied(string action)
    {
        var byName = Actions.ToDictionary(a => a.Name, StringComparer.Ordinal);
        var closure = new HashSet<string>(StringComparer.Ordinal);
        if (!byName.ContainsKey(action))
        {
            return closure;
        }

        var stack = new Stack<string>();
        stack.Push(action);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!closure.Add(current)) continue;
            if (!byName.TryGetValue(current, out var def)) continue;
            foreach (var implied in def.Implies)
            {
                if (!closure.Contains(implied))
                {
                    stack.Push(implied);
                }
            }
        }

        return closure;
    }

    /// <summary>
    /// Returns the closure of an action under the <i>reverse</i> implies graph
    /// — the action itself plus every other action that transitively implies it.
    /// <para>Used by <b>deny</b> overrides: denying <c>EditClose</c> must also
    /// deny <c>Delete</c> and <c>Open</c>, since either would otherwise grant
    /// the user the EditClose capability transitively. The asymmetry with
    /// <see cref="ExpandImplied"/> is deliberate — allow grants downward through
    /// the implies graph, deny grants upward.</para>
    /// </summary>
    public IReadOnlySet<string> ExpandReverseImplied(string action)
    {
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var declared = Actions.Select(a => a.Name).ToHashSet(StringComparer.Ordinal);
        if (!declared.Contains(action)) return closure;

        // For each action A, A's reverse-set includes every action B whose
        // forward closure contains A. Computed eagerly per call — manifest
        // shapes are tiny (handful of actions per resource).
        foreach (var candidate in Actions)
        {
            if (string.Equals(candidate.Name, action, StringComparison.Ordinal))
            {
                closure.Add(candidate.Name);
                continue;
            }
            var forward = ExpandImplied(candidate.Name);
            if (forward.Contains(action))
            {
                closure.Add(candidate.Name);
            }
        }
        return closure;
    }
}
