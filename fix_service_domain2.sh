#!/bin/bash
sed -i 's/public Service Service { get; set; } = null!;/public CapitalUniversity.Core.Domain.Services.Service Service { get; set; } = null!;/g' src/2.Core/CapitalUniversity.Core.Domain/Authorization/RolePermission.cs
sed -i 's/public Service Service { get; set; } = null!;/public CapitalUniversity.Core.Domain.Services.Service Service { get; set; } = null!;/g' src/2.Core/CapitalUniversity.Core.Domain/Authorization/StaffPermissionOverride.cs
mkdir -p src/2.Core/CapitalUniversity.Core.Domain/Services
cat << 'INNER_EOF' > src/2.Core/CapitalUniversity.Core.Domain/Services/Service.cs
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Domain.Services;

public class Service : BaseEntity
{
    public string Name { get; set; } = string.Empty;
}
INNER_EOF
