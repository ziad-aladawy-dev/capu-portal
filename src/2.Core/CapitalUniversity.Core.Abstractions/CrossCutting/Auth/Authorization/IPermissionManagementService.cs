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
    /// Manually-triggered expiry sweep. An override's temporal window ends at the
    /// <c>EndDate</c> of its scoped Semester (or AcademicYear when only a year is
    /// scoped), which is stamped onto <c>ExpiresAt</c> at write time. This call
    /// hard-deletes every override whose <c>ExpiresAt</c> is now-or-past and
    /// rotates the cache version of each affected user. Global / AlwaysActive
    /// overrides carry a null <c>ExpiresAt</c> and are never touched. Returns the
    /// number of override rows deleted.
    /// </summary>
    Task<int> ExpireOverridesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// One-time migration for override rows written before <c>ExpiresAt</c> was
    /// stamped. For every row with a null <c>ExpiresAt</c> that is scoped to a
    /// bounded Semester/AcademicYear, re-derives the end of that window and fills
    /// it in so the expiry sweep and read-time filter can act on legacy rows.
    /// Genuinely Global / AlwaysActive rows are left null. Returns the number of
    /// rows updated.
    /// </summary>
    Task<int> BackfillOverrideExpiryAsync(CancellationToken cancellationToken = default);

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
