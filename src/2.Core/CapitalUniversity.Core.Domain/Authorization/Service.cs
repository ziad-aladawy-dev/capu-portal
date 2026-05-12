using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Authorization;

namespace CapitalUniversity.Core.Domain.Authorization;

public class Service : BaseEntity
{
    public Guid ModuleId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public Module Module { get; set; } = null!;
}