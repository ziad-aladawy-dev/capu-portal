
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class Role : BaseEntity
{
    // L12 — Default to empty so the property is never observably null even
    // when EF materialises the entity before any setter runs. Resolves the
    // CS8618 warning without forcing a `required` keyword that would break
    // EF's parameterless-construction path.
    public string Name { get; set; } = string.Empty;
    public bool IsSystemRole { get; set; }
}