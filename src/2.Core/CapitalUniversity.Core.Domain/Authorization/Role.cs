
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class Role : BaseEntity
{
    public string Name { get; set; }
    public bool IsSystemRole { get; set; }
}