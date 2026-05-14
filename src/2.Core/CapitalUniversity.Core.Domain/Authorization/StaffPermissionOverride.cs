using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using Staff = CapitalUniversity.Core.Domain.Identity.Staff;

namespace CapitalUniversity.Core.Domain.Authorization
{

    public class StaffPermissionOverride : BaseEntity, IUserPermissionOverride
    {
        public Guid StaffId { get; set; }

        public Guid ServiceId { get; set; }
        public Guid PermissionId { get; set; }
        public string Resource { get; set; } = string.Empty;
        public ActionLevel Level { get; set; }
        public Guid? StructureNodeId { get; set; }
        public string? StructureNodePath { get; set; }
        public string Domain { get; set; } = string.Empty;
        public string Year { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public OverrideType Type { get; set; }
        public DateTime? ExpiresAt { get; set; }

        // NOTE: navigation retained for compatibility; may be reviewed during modular separation
        public Staff Staff { get; set; } = null!;
        public Service Service { get; set; } = null!;
        public ICollection<StaffPermissionScope> Scopes { get; set; } = new List<StaffPermissionScope>();

        // For EF core / parameterless instantiation
        protected StaffPermissionOverride() { }

        public StaffPermissionOverride(Guid staffId, Guid serviceId, string resource, ActionLevel level, OverrideType type, string domain, string year, string semester)
        {
            StaffId = staffId;
            ServiceId = serviceId;
            Resource = resource;
            Level = level;
            Domain = domain;
            Type = type;
            Year = year;
            Semester = semester;
        }

        public void UpdateLevel(ActionLevel newLevel)
        {
            Level = newLevel;
        }
    }
}