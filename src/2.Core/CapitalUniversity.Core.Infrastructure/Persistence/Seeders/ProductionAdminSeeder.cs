using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

/// <summary>
/// B3 — Bootstraps a SINGLE Super Admin from configuration for environments where
/// the demo data (which includes the built-in admin) is disabled — i.e.
/// production. Credentials come from <c>Seeding:Admin:*</c>, never hardcoded.
///
/// <para>Behaviour (all guards, fail-safe):</para>
/// <list type="bullet">
///   <item>No-ops if any "Super Admin" staff already exists (so dev/test, where the
///   demo Super Admin is seeded, are untouched).</item>
///   <item>No-ops with a warning if <c>Seeding:Admin:NationalId</c> or
///   <c>Seeding:Admin:Password</c> are not configured — it never invents a
///   default-credential admin.</item>
/// </list>
/// The Super Admin role's permissions come from <c>SeedRolePermissionsAsync</c>
/// (grant-all), which is platform seeding and always runs.
/// </summary>
public static class ProductionAdminSeeder
{
    public static async Task SeedAsync(
        CoreDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration)
    {
        var nationalId = configuration["Seeding:Admin:NationalId"];
        var password = configuration["Seeding:Admin:Password"];

        if (string.IsNullOrWhiteSpace(nationalId) || string.IsNullOrWhiteSpace(password))
        {
            // Only warn when there is genuinely no administrator to log in with.
            if (!await context.Staffs.AnyAsync(s => s.Role == "Super Admin"))
            {
                Console.WriteLine(
                    "[Seed] ProductionAdminSeeder: no Super Admin exists and Seeding:Admin:NationalId/Password " +
                    "are not configured — NO administrator was bootstrapped. Set those settings to create one.");
            }
            return;
        }

        // A Super Admin already exists (e.g. demo-seeded dev/test) — never duplicate.
        if (await context.Staffs.AnyAsync(s => s.Role == "Super Admin"))
        {
            return;
        }

        var superAdminRole = (await context.Roles.ToListAsync())
            .FirstOrDefault(r => LocalizedJson.Extract(r.Name, "en") == "Super Admin");
        if (superAdminRole is null)
        {
            Console.WriteLine("[Seed] ProductionAdminSeeder: 'Super Admin' role not found — cannot bootstrap admin.");
            return;
        }

        // Root structure node (University) for the staff row's home node.
        var rootNode = await context.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.University);
        if (rootNode is null)
        {
            Console.WriteLine("[Seed] ProductionAdminSeeder: no University structure node — cannot bootstrap admin.");
            return;
        }

        var name = configuration["Seeding:Admin:Name"] ?? "System Administrator";
        var email = configuration["Seeding:Admin:Email"] ?? "admin@capital.local";
        var employeeCode = configuration["Seeding:Admin:EmployeeCode"] ?? "ADMIN-001";

        var admin = new Staff
        {
            Id = Guid.NewGuid(),
            EmployeeCode = employeeCode,
            Name = name,
            NationalId = nationalId,
            BirthDate = new DateTime(1990, 1, 1),
            PhoneNumber = configuration["Seeding:Admin:Phone"] ?? "0000000000",
            Email = email,
            Role = "Super Admin",
            JobTitle = LocalizedJson.Of("مسؤول النظام", "System Administrator"),
            StructureNodeId = rootNode.Id,
            PasswordHash = passwordHasher.HashPassword(password),
            // Conservative expiry: the operator should rotate after first login.
            PasswordExpiry = DateTime.UtcNow.AddYears(1),
            IsActive = true,
        };
        context.Staffs.Add(admin);
        await context.SaveChangesAsync();

        // Global Super Admin role assignment (no structural/temporal restriction).
        context.StaffRoles.Add(new StaffRoleAssignment(admin.Id, superAdminRole.Id, ScopeKeys.Global, ScopeKeys.Global)
        {
            StructureNodeId = null,
            StructureNodePath = null,
        });
        await context.SaveChangesAsync();

        Console.WriteLine($"[Seed] ProductionAdminSeeder: bootstrapped Super Admin '{employeeCode}' from configuration.");
    }
}
