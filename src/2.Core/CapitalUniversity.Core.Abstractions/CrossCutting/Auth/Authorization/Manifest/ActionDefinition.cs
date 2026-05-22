namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.Manifest;

/// <summary>
/// One verb a caller can perform on a resource (e.g. <c>"View"</c>, <c>"Insert"</c>,
/// <c>"EditClose"</c>, <c>"Open"</c>, <c>"Delete"</c>). Pairs with the enclosing
/// <see cref="ResourceDefinition"/> and the module key to form the canonical
/// <c>{module}.{resource}.{action}</c> identity.
/// <para>
/// Inheritance is <b>explicit</b> and <b>resource-local</b>: an action grants the
/// caller everything in <see cref="Implies"/> as well, but only when that resource's
/// manifest declares it. There is no global CRUD ladder anymore — a resource that
/// wants Delete to grant View must say so.
/// </para>
/// </summary>
public sealed record ActionDefinition
{
    public string Name { get; init; } = string.Empty;
    public int OrderNumber { get; init; }

    /// <summary>
    /// Resource-local action names that this action grants in addition to itself.
    /// Expanded transitively at lookup time; cycles are silently broken on the
    /// first revisit. Names refer to other <see cref="ActionDefinition.Name"/>
    /// values <i>inside the same resource</i>.
    /// </summary>
    public IReadOnlyCollection<string> Implies { get; init; } = Array.Empty<string>();

    /// <summary>
    /// UI hint: this action is destructive / privileged and should render with
    /// extra confirmation. Not consulted by the runtime authorization decision.
    /// </summary>
    public bool IsDangerous { get; init; }

    /// <summary>
    /// True for the CRUD-like quintet (View / Insert / EditClose / Open / Delete)
    /// where the manifest follows the legacy ladder semantics. Pure metadata;
    /// the runtime treats every action uniformly via <see cref="Implies"/>.
    /// </summary>
    public bool IsHierarchical { get; init; }

    /// <summary>Optional ordering hint for permission-tree rendering.</summary>
    public int? DisplayOrder { get; init; }

    public static ActionDefinition Of(string name, int orderNumber = 0) =>
        new() { Name = name, OrderNumber = orderNumber };

    /// <summary>
    /// Declare an explicit-only action that does NOT inherit from anything.
    /// Examples: Approve, Publish, AssignRole, OverrideGrades, Export, Refund.
    /// </summary>
    public static ActionDefinition Explicit(string name, int orderNumber = 0, bool dangerous = false) =>
        new() { Name = name, OrderNumber = orderNumber, IsDangerous = dangerous };

    /// <summary>
    /// Declare an action that implies a resource-local set of other actions.
    /// </summary>
    public static ActionDefinition Hierarchical(string name, int orderNumber, params string[] implies) =>
        new()
        {
            Name = name,
            OrderNumber = orderNumber,
            Implies = implies,
            IsHierarchical = true,
        };
}
