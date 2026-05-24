using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization.DTOs;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication.DTOs;


namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

public interface IPermissionManagementService
{
    Task<List<PermissionDto>> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetPermissionLookupAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<LoginResponseDto> GetBootstrapContextAsync(IUserCredential user, CancellationToken cancellationToken = default);

    Task<PermissionAssignmentResponse?> GetAssignmentAsync(GetPermissionAssignmentQueryDto query, CancellationToken cancellationToken = default);

    Task<PermissionAssignmentResponse> CreateAssignmentAsync(CreatePermissionAssignmentRequest request, CancellationToken cancellationToken = default);

    Task<PermissionAssignmentResponse> UpdateAssignmentAsync(UpdatePermissionAssignmentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 3.3 — bulk-create assignments. All-or-nothing: any single failure rolls
    /// back the whole batch. Use case: seeding role-permission rows for a new
    /// module so the system never lands in a partially-permissioned state.
    /// </summary>
    Task<IReadOnlyList<PermissionAssignmentResponse>> BatchCreateAssignmentsAsync(IReadOnlyList<CreatePermissionAssignmentRequest> requests, CancellationToken cancellationToken = default);

    /// <summary>
    /// 3.4 — bulk-update / revoke assignments. All-or-nothing transaction.
    /// Each <see cref="UpdatePermissionAssignmentRequest"/> can both add and
    /// remove roles + permissions; this endpoint just applies many in one shot.
    /// </summary>
    Task<IReadOnlyList<PermissionAssignmentResponse>> BatchUpdateAssignmentsAsync(IReadOnlyList<UpdatePermissionAssignmentRequest> requests, CancellationToken cancellationToken = default);
}
