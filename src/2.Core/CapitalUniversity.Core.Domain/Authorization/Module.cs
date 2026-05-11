using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Authorization;

public class Module : BaseEntity
{
    public string ModuleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public int? OrderNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Service> Services { get; set; } = new List<Service>();

}