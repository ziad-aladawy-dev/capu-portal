using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using Staff = CapitalUniversity.Core.Domain.Identity.Staff;

namespace CapitalUniversity.Core.Domain.Authorization
{

    /// <summary>
    /// Per-action override for a single staff member. <see cref="Action"/> names
    /// one verb on <see cref="Resource"/>; <see cref="Type"/> says whether the
    /// row grants or denies it. Effective permissions evaluate as
    /// <c>(allow ∪ implied(allow)) − (deny ∪ implied(deny))</c> against the
    /// manifest's per-resource implies graph — no integer ladder, no arithmetic.
    /// </summary>
    public class StaffPermissionOverride : BaseEntity, IUserPermissionOverride
    {
        public Guid StaffId { get; set; }

        public Guid ResourceId { get; set; }
        public string Action { get; set; } = string.Empty;
        public Guid? StructureNodeId { get; set; }
        public string? StructureNodePath { get; set; }
        public string Year { get; set; } = string.Empty;
        public string Semester { get; set; } = string.Empty;
        public OverrideType Type { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public Staff Staff { get; set; } = null!;
        public Resource Resource { get; set; } = null!;

        // For EF core / parameterless instantiation
        protected StaffPermissionOverride() { }

        public StaffPermissionOverride(Guid staffId, Guid resourceId, string action, OverrideType type, string year, string semester)
        {
            StaffId = staffId;
            ResourceId = resourceId;
            Action = action;
            Type = type;
            Year = year;
            Semester = semester;
        }
    }
}
