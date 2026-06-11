using System;
using System.Collections.Generic;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;

/// <summary>
/// The fixed set of permissions a Student principal holds IMPLICITLY over their
/// OWN records, without any role grant.
///
/// <para>
/// Students are context-scoped: there is no student role-assignment table and the
/// system never grants them role permissions (see PermissionManagementService —
/// "Students are context-scoped: no explicit permission grants"). Before this set
/// existed, the student-facing controllers used <c>[HasPermission(View)]</c> with
/// no way for a student to satisfy it, so every student request 403'd — students
/// could authenticate but never reach the portal (the "unauthorized login loop").
/// </para>
///
/// <para>
/// The authorization handler treats membership here as "permission held" for an
/// <c>IsStudent</c> caller. Self-access is STILL enforced: these endpoints either
/// bind the studentId from the JWT (ResolveStudentId) or carry a
/// <see cref="PermissionScopeKind.Student"/> binding that <c>EffectiveScope</c>
/// checks against the caller's own id. A student therefore gains these verbs over
/// their own data only — never another student's, and never any staff/ops resource.
/// </para>
/// </summary>
public static class StudentSelfPermissions
{
    // Entries are stored in CANONICAL form (PermissionIdentity.Parse) because the
    // authorization handler compares against requirement.Permission, which is
    // PermissionIdentity.Create(...)-normalized (PascalCased, separators stripped:
    // "academic-records.grades.View" -> "AcademicRecords.Grades.View"). Comparing
    // the raw constants would never match.
    private static readonly HashSet<string> Permissions = new(StringComparer.Ordinal)
    {
        // Academic record reads (own grades / transcript / registrations).
        PermissionIdentity.Parse(PermissionNames.Grades.View),
        PermissionIdentity.Parse(PermissionNames.Transcript.View),
        PermissionIdentity.Parse(PermissionNames.RegisteredCourses.View),

        // Own profile records (sparse JSON profile completion).
        PermissionIdentity.Parse(PermissionNames.StudentProfileRecords.View),
        PermissionIdentity.Parse(PermissionNames.StudentProfileRecords.Insert),
        PermissionIdentity.Parse(PermissionNames.StudentProfileRecords.EditClose),

        // NOTE: self-service payment permissions (payments.orders.*) are added
        // in the B8 increment, alongside the manifest resource + controller
        // re-pointing, so they land and verify together.
    };

    /// <summary>True if <paramref name="permission"/> is an implicit student self-permission.</summary>
    public static bool Contains(string permission) => Permissions.Contains(permission);
}
