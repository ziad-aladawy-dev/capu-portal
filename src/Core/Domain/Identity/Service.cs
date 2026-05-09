using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Identity;

public class Service : BaseEntity
{
    public Guid ModuleId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Module Module { get; set; } = null!;
}