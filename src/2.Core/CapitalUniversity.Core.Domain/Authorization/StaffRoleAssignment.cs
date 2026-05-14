using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Authorization;

public class StaffRoleAssignment : BaseEntity
{
    public Guid StaffId { get; set; }
    public Guid RoleId { get; private set; }
    public Guid? StructureNodeId { get; set; }
    public string? StructureNodePath { get; set; }
    public string Year { get; private set; }
    public string Semester { get; private set; }

    public StaffRoleAssignment(Guid staffId, Guid roleId, string year, string semester)
    {
        StaffId = staffId;
        RoleId = roleId;
        Year = year;
        Semester = semester;
    }
}