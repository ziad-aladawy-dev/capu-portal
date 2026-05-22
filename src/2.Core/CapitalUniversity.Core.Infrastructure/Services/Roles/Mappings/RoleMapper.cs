using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Services.Roles.Commands;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Infrastructure.Services.Roles.Mappings;

[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.None,
    AllowNullPropertyAssignment = false)]
public partial class RoleMapper
{
    /// <summary>
    /// PATCH-style sparse update. Omitted (null) source fields are ignored;
    /// only provided values overwrite target state.
    /// </summary>
    public partial void ApplyUpdate(UpdateRoleRequest request, Role entity);
}
