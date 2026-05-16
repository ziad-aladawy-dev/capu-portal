using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        // Tables that are "one-shot" (skip if any rows exist).
        await RunOnceAsync("StructureNodes",  context.StructureNodes,  () => SeedStructureAsync(context));
        await RunOnceAsync("AcademicYears",   context.AcademicYears,   () => SeedAcademicTimelineAsync(context));
        await RunOnceAsync("Modules",         context.Modules,         () => SeedAuthModulesAsync(context));
        await RunOnceAsync("Services",        context.Services,        () => SeedAuthServicesAsync(context));
        await RunOnceAsync("Roles",           context.Roles,           () => SeedRolesAsync(context));

        // Idempotent / self-healing: always run, upsert by natural key so stale state converges.
        await RunStepAsync("RolePermissions", () => SeedRolePermissionsAsync(context));
        await RunStepAsync("Staffs",          () => SeedStaffAsync(context, passwordHasher));
        await RunStepAsync("Students",        () => SeedStudentsAsync(context, passwordHasher));
        await RunStepAsync("StaffRoles",      () => SeedStaffRoleAssignmentsAsync(context));
        await RunOnceAsync("Notifications",   context.Notifications,   () => SeedNotificationsAsync(context));
    }

    private static async Task RunOnceAsync<T>(string name, DbSet<T> set, Func<Task> seed) where T : class
    {
        if (await set.AnyAsync())
        {
            Console.WriteLine($"[Seed] {name}: already populated, skipping.");
            return;
        }
        await RunStepAsync(name, seed);
    }

    private static async Task RunStepAsync(string name, Func<Task> seed)
    {
        try
        {
            await seed();
            Console.WriteLine($"[Seed] {name}: OK.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Seed] {name}: FAILED — {ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  1. UNIVERSITY STRUCTURE
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStructureAsync(CoreDbContext context)
    {
        // Locally-scoped collector — must NOT be a static field or concurrent test
        // seedings (each on its own in-memory DB) race on it and throw
        // InvalidOperationException: "Collection was modified".
        var nodes = new List<StructureNode>();

        StructureNode MakeNode(string name, StructureNodeType type, StructureNode? parent, int order)
        {
            var node = new StructureNode
            {
                Id = Guid.NewGuid(), Name = name, Type = type,
                ParentId = parent?.Id, Order = order,
                Depth = parent?.Depth + 1 ?? 0, IsActive = true,
            };
            node.Path = parent == null ? $"/{node.Id}" : $"{parent.Path}/{node.Id}";
            nodes.Add(node);
            return node;
        }

        void MakeLevelsInt(StructureNode parent, params int[] years)
        {
            foreach (var y in years) MakeNode($"Level {y}", StructureNodeType.Level, parent, y - 1);
        }

        void MakeLevelsStr(StructureNode parent, params string[] names)
        {
            for (var i = 0; i < names.Length; i++) MakeNode(names[i], StructureNodeType.Level, parent, i);
        }

        var u = MakeNode("Capital University", StructureNodeType.University, null, 0);

        var he = MakeNode("Faculty of Home Economics", StructureNodeType.Faculty, u, 0);
        var heCredit = MakeNode("Credit Hours System", StructureNodeType.System, he, 0);
        var heSemester = MakeNode("Semester System", StructureNodeType.System, he, 1);

        MakeLevelsInt(MakeNode("Clinical Nutrition Program", StructureNodeType.Program, heCredit, 0), 1, 2, 3, 4);
        MakeLevelsInt(MakeNode("Family & Childhood Institution Management", StructureNodeType.Program, heCredit, 1), 1, 2, 3, 4);
        var nutrition = MakeNode("Nutrition & Food Science", StructureNodeType.Program, heCredit, 2);
        MakeLevelsInt(nutrition, 1, 2, 3, 4);
        MakeLevelsInt(MakeNode("Textile & Clothing", StructureNodeType.Program, heCredit, 3), 1, 2, 3, 4);
        MakeLevelsInt(MakeNode("General Stream", StructureNodeType.Program, heCredit, 4), 1, 2, 3, 4);
        MakeLevelsInt(MakeNode("Leather Industries", StructureNodeType.Program, heCredit, 5), 1, 2, 3, 4);

        var mat = MakeNode("Faculty of Engineering – Mataria", StructureNodeType.Faculty, u, 1);
        var matCredit = MakeNode("Credit Hours System", StructureNodeType.System, mat, 0);
        MakeLevelsStr(MakeNode("Civil Engineering", StructureNodeType.Program, matCredit, 0), "Freshman", "Sophomore", "Junior", "Senior");
        MakeLevelsStr(MakeNode("Architectural Engineering", StructureNodeType.Program, matCredit, 1), "Freshman", "Sophomore", "Junior", "Senior");
        MakeLevelsStr(MakeNode("Mechanical Engineering", StructureNodeType.Program, matCredit, 2), "Freshman", "Sophomore", "Junior", "Senior-1", "Senior-2");
        MakeLevelsStr(MakeNode("Electrical Engineering", StructureNodeType.Program, matCredit, 3), "Freshman", "Sophomore", "Junior", "Senior");

        var hel = MakeNode("Faculty of Engineering – Helwan", StructureNodeType.Faculty, u, 2);
        var helCredit = MakeNode("Credit Hours System", StructureNodeType.System, hel, 0);
        MakeLevelsStr(MakeNode("Computer & Systems Engineering", StructureNodeType.Program, helCredit, 0), "Sophomore", "Junior", "Senior");
        MakeLevelsStr(MakeNode("Communications & Information Engineering", StructureNodeType.Program, helCredit, 1), "Preparatory", "First", "Second", "Third", "Fourth");
        MakeLevelsStr(MakeNode("Biomedical Engineering", StructureNodeType.Program, helCredit, 2), "Freshman", "Sophomore", "Junior", "Senior");
        MakeLevelsStr(MakeNode("Industrial Engineering", StructureNodeType.Program, helCredit, 3), "Freshman", "Sophomore", "Junior", "Senior");

        await context.StructureNodes.AddRangeAsync(nodes);
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  2. ACADEMIC TIMELINE
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedAcademicTimelineAsync(CoreDbContext context)
    {
        var yearDefs = new[]
        {
            ("2023-2024", new DateTime(2023, 9, 1), new DateTime(2024, 8, 31), false),
            ("2024-2025", new DateTime(2024, 9, 1), new DateTime(2025, 8, 31), false),
            ("2025-2026", new DateTime(2025, 9, 1), new DateTime(2026, 8, 31), true),
            ("2026-2027", new DateTime(2026, 9, 1), new DateTime(2027, 8, 31), false),
        };

        foreach (var (name, start, end, current) in yearDefs)
        {
            var ay = new AcademicYear { Id = Guid.NewGuid(), Name = name, StartDate = start, EndDate = end, IsCurrent = current };
            ay.Semesters = new List<Semester>
            {
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = "Fall",   Order = 1, StartDate = start, EndDate = start.AddMonths(4),  IsCurrent = current },
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = "Spring", Order = 2, StartDate = start.AddMonths(5), EndDate = start.AddMonths(9), IsCurrent = false },
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = "Summer", Order = 3, StartDate = start.AddMonths(10),EndDate = end,                IsCurrent = false },
            };
            context.AcademicYears.Add(ay);
        }
        await context.SaveChangesAsync();
    }



    // ════════════════════════════════════════════════════════════════
    //  4. AUTH MODULES
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedAuthModulesAsync(CoreDbContext context)
    {
        var modules = new[]
        {
            ("dashboard",   "Dashboard",            "LayoutDashboard", 0),
            ("users",       "User Management",      "Users",           1),
            ("structure",   "University Structure", "Building2",       2),
            ("programs",    "Academic Programs",    "BookOpen",        3),
            ("permissions", "Permissions & Roles",  "Shield",          4),
            ("sync",        "SIS Integration",      "RefreshCw",       5),
            ("academics",   "Academic Timeline",    "Calendar",        6),
            ("notifications","Notifications",       "Bell",            7),
        };
        foreach (var (key, display, icon, order) in modules)
            context.Modules.Add(new Module { Id = Guid.NewGuid(), ModuleKey = key, DisplayName = display, Icon = icon, OrderNumber = order });
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  5. AUTH SERVICES
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedAuthServicesAsync(CoreDbContext context)
    {
        var modules = await context.Modules.ToListAsync();

        void AddSvc(string moduleKey, string displayName, int order)
        {
            var modId = modules.First(m => m.ModuleKey == moduleKey).Id;
            context.Services.Add(new Service { Id = Guid.NewGuid(), ModuleId = modId, DisplayName = displayName, OrderNumber = order });
        }

        AddSvc("dashboard",    "View Dashboard",       0);
        AddSvc("users",        "View Users",           0);
        AddSvc("users",        "Create Users",         1);
        AddSvc("users",        "Edit Users",           2);
        AddSvc("users",        "Delete Users",         3);
        AddSvc("structure",    "View Structure",       0);
        AddSvc("structure",    "Manage Structure",     1);
        AddSvc("programs",     "View Programs",        0);
        AddSvc("programs",     "Manage Programs",      1);
        AddSvc("permissions",  "View Permissions",     0);
        AddSvc("permissions",  "Manage Permissions",   1);
        AddSvc("sync",         "View Sync",            0);
        AddSvc("sync",         "Execute Sync",         1);
        AddSvc("academics",    "View Academic Years",  0);
        AddSvc("academics",    "Manage Semesters",     1);
        AddSvc("notifications","View Notifications",   0);
        AddSvc("notifications","Send Notifications",   1);
        AddSvc("permissions",  "Manage Roles",         2);

        await context.SaveChangesAsync();
    }
    
    // ════════════════════════════════════════════════════════════════
    //  6. ROLES
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedRolesAsync(CoreDbContext context)
    {
        var roles = new[]
        {
            ("Super Admin",      true),
            ("Faculty Admin",    false),
            ("Department Head",  false),
            ("Registrar",        false),
            ("Academic Advisor", false),
            ("Staff",            false),
            ("Viewer",           false),
        };
        foreach (var (name, system) in roles)
            context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = name, IsSystemRole = system });
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  7. ROLE PERMISSIONS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedRolePermissionsAsync(CoreDbContext context)
    {
        var roles = await context.Roles.ToListAsync();
        var svcList = await context.Services.ToListAsync();
        await context.Modules.LoadAsync();

        var roleMap = roles.ToDictionary(r => r.Name, r => r.Id);

        // Load existing rows from the DB (not just .Local) so the upsert respects
        // the IX_RolePermissions_RoleId_ServiceId unique index across runs.
        var existing = await context.RolePermissions
            .ToDictionaryAsync(rp => (rp.RoleId, rp.ServiceId));

        void AddPerm(string roleName, string displayName, ActionLevel level)
        {
            var svc = svcList.FirstOrDefault(s => s.DisplayName == displayName);
            if (svc == null) return;

            var mod = context.Modules.Local.First(m => m.Id == svc.ModuleId);
            var resource = PermissionIdentity.ResourceFor(mod.ModuleKey, displayName);
            var roleId = roleMap[roleName];

            if (existing.TryGetValue((roleId, svc.Id), out var current))
            {
                if (level > current.Level) current.Level = level;
                current.Resource = resource;
                return;
            }

            var newRow = new RolePermission(roleId, svc.Id, resource, level)
            {
                Id = Guid.NewGuid(),
                PermissionId = Guid.NewGuid(),
            };
            context.RolePermissions.Add(newRow);
            existing[(roleId, svc.Id)] = newRow;
        }

        // Super Admin — full access
        foreach (var svc in svcList)
            AddPerm("Super Admin", svc.DisplayName, ActionLevel.Delete);

        // Faculty Admin
        AddPerm("Faculty Admin", "View Dashboard",       ActionLevel.View);
        AddPerm("Faculty Admin", "View Users",           ActionLevel.EditClose);
        AddPerm("Faculty Admin", "Create Users",         ActionLevel.Insert);
        AddPerm("Faculty Admin", "Edit Users",           ActionLevel.EditClose);
        AddPerm("Faculty Admin", "View Structure",       ActionLevel.View);
        AddPerm("Faculty Admin", "View Programs",        ActionLevel.View);
        AddPerm("Faculty Admin", "Manage Programs",      ActionLevel.Insert);
        AddPerm("Faculty Admin", "View Permissions",     ActionLevel.View);
        AddPerm("Faculty Admin", "View Academic Years",  ActionLevel.View);
        AddPerm("Faculty Admin", "Manage Semesters",     ActionLevel.View);
        AddPerm("Faculty Admin", "View Notifications",   ActionLevel.View);
        AddPerm("Faculty Admin", "Send Notifications",   ActionLevel.Insert);
        AddPerm("Faculty Admin", "Manage Roles",         ActionLevel.View);

        // Department Head
        AddPerm("Department Head", "View Dashboard",     ActionLevel.View);
        AddPerm("Department Head", "View Users",         ActionLevel.EditClose);
        AddPerm("Department Head", "Edit Users",         ActionLevel.EditClose);
        AddPerm("Department Head", "View Structure",     ActionLevel.View);
        AddPerm("Department Head", "View Programs",      ActionLevel.View);
        AddPerm("Department Head", "View Academic Years",ActionLevel.View);
        AddPerm("Department Head", "View Notifications", ActionLevel.View);

        // Registrar
        AddPerm("Registrar", "View Dashboard",           ActionLevel.View);
        AddPerm("Registrar", "View Users",               ActionLevel.View);
        AddPerm("Registrar", "Create Users",             ActionLevel.Insert);
        AddPerm("Registrar", "Edit Users",               ActionLevel.EditClose);
        AddPerm("Registrar", "View Structure",           ActionLevel.View);
        AddPerm("Registrar", "View Programs",            ActionLevel.View);
        AddPerm("Registrar", "Manage Programs",          ActionLevel.Insert);
        AddPerm("Registrar", "View Academic Years",      ActionLevel.View);
        AddPerm("Registrar", "Manage Semesters",         ActionLevel.View);

        // Academic Advisor
        AddPerm("Academic Advisor", "View Dashboard",    ActionLevel.View);
        AddPerm("Academic Advisor", "View Users",        ActionLevel.View);
        AddPerm("Academic Advisor", "View Structure",    ActionLevel.View);
        AddPerm("Academic Advisor", "View Programs",     ActionLevel.View);
        AddPerm("Academic Advisor", "View Notifications",ActionLevel.View);

        // Staff (basic)
        AddPerm("Staff", "View Dashboard",               ActionLevel.View);
        AddPerm("Staff", "View Users",                   ActionLevel.View);
        AddPerm("Staff", "View Notifications",           ActionLevel.View);

        // Viewer
        AddPerm("Viewer", "View Dashboard",              ActionLevel.View);
        AddPerm("Viewer", "View Users",                  ActionLevel.View);

        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  8. STAFF  (Super admin credentials: nationalId=29801011234567, password=admin123)
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStaffAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        var nodes = await context.StructureNodes.ToListAsync();
        StructureNode? FindNode(string name) => nodes.FirstOrDefault(n => n.Name.Contains(name));

        var adminPwd = passwordHasher.HashPassword("admin123");
        var defs = new (string Emp, string Name, string NID, DateTime DOB, string Phone, string Email, string Role, string Job, string NodeName)[]
        {
            ("ADMIN-001", "Super Admin User",      "29801011234567", new(1985, 6, 15),  "01000000000", "superadmin@capital.edu.eg",        "Super Admin",      "System Administrator",                "Capital University"),
            ("FAC-001",   "Dr. Fatima Hassan",     "28501011234567", new(1978, 3, 20),  "01111111111", "fatima.hassan@capital.edu.eg",     "Faculty Admin",    "Faculty Dean",                        "Home Economics"),
            ("HOD-001",   "Dr. Khaled Ibrahim",    "28102021234567", new(1975, 11, 5),  "01111111112", "khaled.ibrahim@capital.edu.eg",    "Department Head",  "Head of Clinical Nutrition",          "Clinical Nutrition"),
            ("REG-001",   "Ms. Aisha Mahmoud",     "29003031234567", new(1985, 7, 12),  "01111111113", "aisha.mahmoud@capital.edu.eg",     "Registrar",        "Senior Registrar Officer",            "Capital University"),
            ("ADV-001",   "Dr. Omar El-Sayed",     "27504041234567", new(1982, 9, 28),  "01111111114", "omar.elsayed@capital.edu.eg",      "Academic Advisor", "Student Academic Advisor",            "Nutrition & Food Science"),
            ("STF-001",   "Mr. Tamer Said",        "29205051234567", new(1990, 1, 15),  "01111111115", "tamer.said@capital.edu.eg",        "Staff",            "IT Support Specialist",               "Capital University"),
            ("VWR-001",   "Ms. Nadia Youssef",     "29306061234567", new(1992, 4, 8),   "01111111116", "nadia.youssef@capital.edu.eg",     "Viewer",           "External Auditor",                    "Capital University"),
            ("FAC-002",   "Dr. Ahmed Abdel-Rahman","27807071234567", new(1970, 8, 18),  "01111111117", "ahmed.abdelrahman@capital.edu.eg", "Faculty Admin",    "Vice Dean for Academic Affairs",      "Mataria"),
        };

        var existing = await context.Staffs.ToDictionaryAsync(s => s.EmployeeCode);
        var added = 0;
        var updated = 0;
        foreach (var d in defs)
        {
            var node = FindNode(d.NodeName);
            if (node == null)
            {
                Console.WriteLine($"[Seed] Staffs: skipping {d.Emp} — structure node '{d.NodeName}' not found.");
                continue;
            }

            if (existing.TryGetValue(d.Emp, out var current))
            {
                current.Name = d.Name;
                current.NationalId = d.NID;
                current.BirthDate = d.DOB;
                current.PhoneNumber = d.Phone;
                current.Email = d.Email;
                current.Role = d.Role;
                current.JobTitle = d.Job;
                current.StructureNodeId = node.Id;
                current.PasswordHash = adminPwd;
                current.PasswordExpiry = DateTime.UtcNow.AddYears(5);
                current.IsActive = true;
                updated++;
            }
            else
            {
                context.Staffs.Add(new Staff
                {
                    Id = Guid.NewGuid(),
                    EmployeeCode = d.Emp,
                    Name = d.Name,
                    NationalId = d.NID,
                    BirthDate = d.DOB,
                    PhoneNumber = d.Phone,
                    Email = d.Email,
                    Role = d.Role,
                    JobTitle = d.Job,
                    StructureNodeId = node.Id,
                    PasswordHash = adminPwd,
                    PasswordExpiry = DateTime.UtcNow.AddYears(5),
                    IsActive = true,
                });
                added++;
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[Seed] Staffs: +{added} added, ~{updated} updated.");
    }

    // ════════════════════════════════════════════════════════════════
    //  9. STUDENTS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStudentsAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        var nodes = await context.StructureNodes.ToListAsync();
        StructureNode? FindNode(string name) => nodes.FirstOrDefault(n => n.Name.Contains(name));

        var pwd = passwordHasher.HashPassword("123456");
        var defs = new (string Code, string Name, string NID, DateTime DOB, string Phone, string Email, string NodeName)[]
        {
            ("20250001", "Ahmed Mohamed Ali",        "30201011234567", new(2002, 1, 1),  "01000000001", "ahmed.mohamed@capital.edu.eg",   "Nutrition & Food Science"),
            ("20250002", "Sara Mahmoud Hassan",      "30202021234567", new(2002, 2, 2),  "01000000002", "sara.hassan@capital.edu.eg",      "Level 4"),
            ("20250003", "Mohamed Khaled Ibrahim",   "30203031234567", new(2003, 3, 3),  "01000000003", "mohamed.ibrahim@capital.edu.eg",  "Level 4"),
            ("20250004", "Nourhan Atef El-Sayed",    "30204041234567", new(2001, 5, 15), "01000000004", "nourhan.atef@capital.edu.eg",     "Level 1"),
            ("20250005", "Mariam Tarek Fathy",       "30205051234567", new(2001, 8, 22), "01000000005", "mariam.tarek@capital.edu.eg",     "Textile & Clothing"),
            ("20250006", "Youssef Gamal El-Din",     "30206061234567", new(2004, 11, 2), "01000000006", "youssef.gamal@capital.edu.eg",    "Textile & Clothing"),
            ("20250007", "Omar Hossam El-Din",       "30207071234567", new(2003, 4, 10), "01000000007", "omar.hossam@capital.edu.eg",      "Civil Engineering"),
            ("20250008", "Laila Sherif Kamal",       "30208081234567", new(2002, 7, 18), "01000000008", "laila.sherif@capital.edu.eg",     "Civil Engineering"),
            ("20250009", "Ali Hassan Mohamed",       "30209091234567", new(2001, 12, 5), "01000000009", "ali.hassan@capital.edu.eg",       "Computer & Systems Engineering"),
            ("20250010", "Hagar Mahmoud Ahmed",      "30210101234567", new(2003, 9, 14), "01000000010", "hagar.mahmoud@capital.edu.eg",    "Computer & Systems Engineering"),
            ("20250011", "Karim Mostafa Abdel-Aziz", "30211111234567", new(2002, 3, 28), "01000000011", "karim.mostafa@capital.edu.eg",    "Computer & Systems Engineering"),
            ("20250012", "Salma Adel Naguib",        "30212121234567", new(2001, 6, 30), "01000000012", "salma.adel@capital.edu.eg",       "Communications & Information Engineering"),
            ("20250013", "Hassan Emad El-Din",       "30213131234567", new(2004, 1, 8),  "01000000013", "hassan.emad@capital.edu.eg",      "Communications & Information Engineering"),
            ("20250014", "Dalia Samir Fawzy",        "30214141234567", new(2003, 10, 20),"01000000014", "dalia.samir@capital.edu.eg",      "Architectural Engineering"),
            ("20250015", "Amr Khaled Youssef",       "30215151234567", new(2002, 2, 14), "01000000015", "amr.khaled@capital.edu.eg",       "Architectural Engineering"),
            ("20250016", "Nada Ashraf Ibrahim",      "30216161234567", new(2001, 11, 11),"01000000016", "nada.ashraf@capital.edu.eg",      "Biomedical Engineering"),
            ("20250017", "Mostafa Gamal Hassan",     "30217171234567", new(2004, 5, 25), "01000000017", "mostafa.gamal@capital.edu.eg",    "Biomedical Engineering"),
        };

        var existing = await context.Students.ToDictionaryAsync(s => s.StudentCode);
        var added = 0;
        var updated = 0;
        foreach (var d in defs)
        {
            var node = FindNode(d.NodeName);
            if (node == null)
            {
                Console.WriteLine($"[Seed] Students: skipping {d.Code} — structure node '{d.NodeName}' not found.");
                continue;
            }

            if (existing.TryGetValue(d.Code, out var current))
            {
                current.Name = d.Name;
                current.NationalId = d.NID;
                current.BirthDate = d.DOB;
                current.PhoneNumber = d.Phone;
                current.Email = d.Email;
                current.StructureNodeId = node.Id;
                current.PasswordHash = pwd;
                current.PasswordExpiry = DateTime.UtcNow.AddYears(5);
                current.IsActive = true;
                updated++;
            }
            else
            {
                context.Students.Add(new Student
                {
                    Id = Guid.NewGuid(),
                    StudentCode = d.Code,
                    Name = d.Name,
                    NationalId = d.NID,
                    BirthDate = d.DOB,
                    PhoneNumber = d.Phone,
                    Email = d.Email,
                    StructureNodeId = node.Id,
                    PasswordHash = pwd,
                    PasswordExpiry = DateTime.UtcNow.AddYears(5),
                    IsActive = true,
                });
                added++;
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[Seed] Students: +{added} added, ~{updated} updated.");
    }

    // ════════════════════════════════════════════════════════════════
    // 10. STAFF ROLE ASSIGNMENTS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStaffRoleAssignmentsAsync(CoreDbContext context)
    {
        var staff = await context.Staffs.ToDictionaryAsync(s => s.EmployeeCode);
        var roles = await context.Roles.ToDictionaryAsync(r => r.Name);
        var nodes = await context.StructureNodes.ToListAsync();
        var years = await context.AcademicYears.Include(y => y.Semesters).ToListAsync();

        var year2526 = years.FirstOrDefault(y => y.Name == "2025-2026");
        var fall = year2526?.Semesters.FirstOrDefault(s => s.Name == "Fall");
        if (year2526 == null || fall == null)
        {
            Console.WriteLine("[Seed] StaffRoles: aborting — academic year '2025-2026' or its 'Fall' semester missing.");
            return;
        }

        var yearKey = year2526.Id.ToString();
        var fallKey = fall.Id.ToString();

        StructureNode? FindNode(string name) => nodes.FirstOrDefault(n => n.Name.Contains(name));

        // NodeName=null means "global structural" — the row has no structural restriction
        // and the runtime PermissionService filter accepts it regardless of header.
        // Reserved for Super Admin; every other role is tenanted to a real node.
        var defs = new (string EmpCode, string RoleName, string Year, string Semester, string? NodeName)[]
        {
            ("ADMIN-001", "Super Admin",      "Global", "Global", null),
            ("FAC-001",   "Faculty Admin",    yearKey,  fallKey,  "Home Economics"),
            ("HOD-001",   "Department Head",  yearKey,  fallKey,  "Clinical Nutrition"),
            ("REG-001",   "Registrar",        yearKey,  fallKey,  "Capital University"),
            ("ADV-001",   "Academic Advisor", yearKey,  fallKey,  "Nutrition & Food Science"),
            ("STF-001",   "Staff",            yearKey,  fallKey,  "Capital University"),
            ("VWR-001",   "Viewer",           yearKey,  fallKey,  "Capital University"),
            ("FAC-002",   "Faculty Admin",    yearKey,  fallKey,  "Mataria"),
        };

        var existing = await context.StaffRoles.ToListAsync();
        var added = 0;
        foreach (var d in defs)
        {
            if (!staff.TryGetValue(d.EmpCode, out var s))
            {
                Console.WriteLine($"[Seed] StaffRoles: skipping {d.EmpCode} — staff row missing.");
                continue;
            }
            if (!roles.TryGetValue(d.RoleName, out var r))
            {
                Console.WriteLine($"[Seed] StaffRoles: skipping {d.EmpCode}/{d.RoleName} — role missing.");
                continue;
            }

            StructureNode? node = null;
            if (d.NodeName != null)
            {
                node = FindNode(d.NodeName);
                if (node == null)
                {
                    Console.WriteLine($"[Seed] StaffRoles: skipping {d.EmpCode} — node '{d.NodeName}' not found.");
                    continue;
                }
            }

            var alreadyAssigned = existing.Any(a =>
                a.StaffId == s.Id &&
                a.RoleId == r.Id &&
                a.StructureNodeId == node?.Id &&
                a.Year == d.Year &&
                a.Semester == d.Semester);

            if (alreadyAssigned) continue;

            context.StaffRoles.Add(new StaffRoleAssignment(s.Id, r.Id, d.Year, d.Semester)
            {
                StructureNodeId = node?.Id,
                StructureNodePath = node?.Path,
            });
            added++;
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[Seed] StaffRoles: +{added} added.");
    }

    // ════════════════════════════════════════════════════════════════
    // 11. NOTIFICATIONS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedNotificationsAsync(CoreDbContext context)
    {
        var staff = await context.Staffs.ToListAsync();
        var adminId = staff.First(s => s.EmployeeCode == "ADMIN-001").Id;
        var regId = staff.First(s => s.EmployeeCode == "REG-001").Id;
        var facId = staff.First(s => s.EmployeeCode == "FAC-001").Id;

        context.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), RecipientUserId = adminId, Title = "Welcome to Capital University Portal", Message = "Your account has been created with Super Administrator privileges.", Type = NotificationType.Info, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = adminId, Title = "Academic Year Update", Message = "The 2025-2026 academic year has been activated. Please review the semester schedule.", Type = NotificationType.Warning, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = adminId, Title = "Pending Approvals", Message = "There are 3 pending registration approvals requiring your attention.", Type = NotificationType.Info, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = regId,  Title = "Bulk Import Complete", Message = "Student data import completed. 47 new records created, 2 duplicates skipped.", Type = NotificationType.Info, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = facId,  Title = "Department Report Due", Message = "The semester-end departmental report is due by December 15.", Type = NotificationType.Warning, IsRead = false }
        );

        await context.SaveChangesAsync();
    }
}