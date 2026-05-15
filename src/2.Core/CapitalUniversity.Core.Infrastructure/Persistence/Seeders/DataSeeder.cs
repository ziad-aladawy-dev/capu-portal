using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using SimpleService = CapitalUniversity.Core.Domain.Authorization.Service;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        if (!await context.StructureNodes.AnyAsync())
            await SeedStructureAsync(context);

        if (!await context.AcademicYears.AnyAsync())
            await SeedAcademicTimelineAsync(context);

        if (!await context.Modules.AnyAsync())
            await SeedAuthModulesAsync(context);

        if (!await context.Services.AnyAsync())
            await SeedAuthServicesAsync(context);

        if (!await context.Roles.AnyAsync())
            await SeedRolesAsync(context);

        if (!await context.RolePermissions.AnyAsync())
            await SeedRolePermissionsAsync(context);

        if (!await context.Staffs.AnyAsync())
            await SeedStaffAsync(context, passwordHasher);

        if (!await context.Students.AnyAsync())
            await SeedStudentsAsync(context, passwordHasher);

        if (!await context.StaffRoles.AnyAsync())
            await SeedStaffRoleAssignmentsAsync(context);

        if (!await context.Notifications.AnyAsync())
            await SeedNotificationsAsync(context);
    }

    // ════════════════════════════════════════════════════════════════
    //  1. UNIVERSITY STRUCTURE
    // ════════════════════════════════════════════════════════════════

    private static readonly List<StructureNode> _nodes = new();

    private static async Task SeedStructureAsync(CoreDbContext context)
    {
        var u = MakeNode("Capital University", StructureNodeType.University, null, 0);

        var he = MakeNode("Faculty of Home Economics", StructureNodeType.Faculty, u, 0);
        var heCredit = MakeNode("Credit Hours System", StructureNodeType.System, he, 0);
        var heSemester = MakeNode("Semester System", StructureNodeType.System, he, 1);

        MakeLevels(MakeNode("Clinical Nutrition Program", StructureNodeType.Program, heCredit, 0), 1, 2, 3, 4);
        MakeLevels(MakeNode("Family & Childhood Institution Management", StructureNodeType.Program, heCredit, 1), 1, 2, 3, 4);
        var nutrition = MakeNode("Nutrition & Food Science", StructureNodeType.Program, heCredit, 2);
        MakeLevels(nutrition, 1, 2, 3, 4);
        MakeLevels(MakeNode("Textile & Clothing", StructureNodeType.Program, heCredit, 3), 1, 2, 3, 4);
        MakeLevels(MakeNode("General Stream", StructureNodeType.Program, heCredit, 4), 1, 2, 3, 4);
        MakeLevels(MakeNode("Leather Industries", StructureNodeType.Program, heCredit, 5), 1, 2, 3, 4);

        var mat = MakeNode("Faculty of Engineering – Mataria", StructureNodeType.Faculty, u, 1);
        var matCredit = MakeNode("Credit Hours System", StructureNodeType.System, mat, 0);
        MakeLevels(MakeNode("Civil Engineering", StructureNodeType.Program, matCredit, 0), "Freshman", "Sophomore", "Junior", "Senior");
        MakeLevels(MakeNode("Architectural Engineering", StructureNodeType.Program, matCredit, 1), "Freshman", "Sophomore", "Junior", "Senior");
        MakeLevels(MakeNode("Mechanical Engineering", StructureNodeType.Program, matCredit, 2), "Freshman", "Sophomore", "Junior", "Senior-1", "Senior-2");
        MakeLevels(MakeNode("Electrical Engineering", StructureNodeType.Program, matCredit, 3), "Freshman", "Sophomore", "Junior", "Senior");

        var hel = MakeNode("Faculty of Engineering – Helwan", StructureNodeType.Faculty, u, 2);
        var helCredit = MakeNode("Credit Hours System", StructureNodeType.System, hel, 0);
        MakeLevels(MakeNode("Computer & Systems Engineering", StructureNodeType.Program, helCredit, 0), "Sophomore", "Junior", "Senior");
        MakeLevels(MakeNode("Communications & Information Engineering", StructureNodeType.Program, helCredit, 1), "Preparatory", "First", "Second", "Third", "Fourth");
        MakeLevels(MakeNode("Biomedical Engineering", StructureNodeType.Program, helCredit, 2), "Freshman", "Sophomore", "Junior", "Senior");
        MakeLevels(MakeNode("Industrial Engineering", StructureNodeType.Program, helCredit, 3), "Freshman", "Sophomore", "Junior", "Senior");

        await context.StructureNodes.AddRangeAsync(_nodes);
        await context.SaveChangesAsync();
    }

    private static void MakeLevels(StructureNode parent, params int[] years)
    {
        foreach (var y in years) MakeNode($"Level {y}", StructureNodeType.Level, parent, y - 1);
    }

    private static void MakeLevels(StructureNode parent, params string[] names)
    {
        for (var i = 0; i < names.Length; i++) MakeNode(names[i], StructureNodeType.Level, parent, i);
    }

    private static StructureNode MakeNode(string name, StructureNodeType type, StructureNode? parent, int order)
    {
        var node = new StructureNode
        {
            Id = Guid.NewGuid(), Name = name, Type = type,
            ParentId = parent?.Id, Order = order,
            Depth = parent?.Depth + 1 ?? 0, IsActive = true,
        };
        node.Path = parent == null ? $"/{node.Id}" : $"{parent.Path}/{node.Id}";
        _nodes.Add(node);
        return node;
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
        var simpleSvcList = await context.Set<SimpleService>().ToListAsync();
        await context.Modules.LoadAsync();

        var roleMap = roles.ToDictionary(r => r.Name, r => r.Id);
        var simpleSvcByName = simpleSvcList.ToDictionary(s => s.DisplayName);

        // Resolve simple service name for FK based on auth service's module
        string SimpleSvcName(Module mod) => mod.ModuleKey switch
        {
            "permissions"  => "permissions",
            "academics"    => "academic-years",
            "notifications"=> "notifications",
            _              => mod.ModuleKey,
        };

        void AddPerm(string roleName, string displayName, ActionLevel level)
        {
            var authSvc = svcList.FirstOrDefault(s => s.DisplayName == displayName);
            if (authSvc == null) return;

            var mod = context.Modules.Local.First(m => m.Id == authSvc.ModuleId);
            var svcName = SimpleSvcName(mod);
            if (!simpleSvcByName.TryGetValue(svcName, out var simpleSvc)) return;

            // Handle "Manage Roles" which should use "roles" simple service
            if (displayName == "Manage Roles")
            {
                if (!simpleSvcByName.TryGetValue("roles", out var rolesSvc)) return;
                simpleSvc = rolesSvc;
            }

            var roleId = roleMap[roleName];

            // Upsert: avoid duplicate (RoleId, ServiceId) per unique index
            var existing = context.RolePermissions.Local
                .FirstOrDefault(rp => rp.RoleId == roleId && rp.ServiceId == simpleSvc.Id);
            if (existing != null)
            {
                var currentLevel = (ActionLevel)context.Entry(existing).Property("Level").CurrentValue;
                if (level > currentLevel)
                    context.Entry(existing).Property("Level").CurrentValue = (int)level;
                return;
            }

            var rp = new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                ServiceId = simpleSvc.Id,
                PermissionId = Guid.NewGuid(),
            };

            context.RolePermissions.Add(rp);

            // Set private-setters via change tracker
            context.Entry(rp).Property("Resource").CurrentValue = svcName;
            context.Entry(rp).Property("Level").CurrentValue = (int)level;
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
        StructureNode Find(string name) => nodes.First(n => n.Name.Contains(name));

        var adminPwd = passwordHasher.HashPassword("admin123");
        var studentPwd = passwordHasher.HashPassword("123456");
        var staffList = new List<Staff>
        {
            new() { Id = Guid.NewGuid(), EmployeeCode = "ADMIN-001", PasswordHash = adminPwd, Name = "Super Admin User",      NationalId = "29801011234567", BirthDate = new(1985, 6, 15),  PhoneNumber = "01000000000", Email = "superadmin@capital.edu.eg", Role = "Super Admin",     JobTitle = "System Administrator",         StructureNodeId = Find("Capital University").Id, PasswordExpiry = DateTime.UtcNow.AddYears(5), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "FAC-001",  PasswordHash = adminPwd, Name = "Dr. Fatima Hassan",      NationalId = "28501011234567", BirthDate = new(1978, 3, 20),  PhoneNumber = "01111111111", Email = "fatima.hassan@capital.edu.eg", Role = "Faculty Admin",   JobTitle = "Faculty Dean",                StructureNodeId = Find("Home Economics").Id,  PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "HOD-001",  PasswordHash = adminPwd, Name = "Dr. Khaled Ibrahim",     NationalId = "28102021234567", BirthDate = new(1975, 11, 5),  PhoneNumber = "01111111112", Email = "khaled.ibrahim@capital.edu.eg", Role = "Department Head", JobTitle = "Head of Clinical Nutrition",  StructureNodeId = Find("Clinical Nutrition").Id, PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "REG-001",  PasswordHash = adminPwd, Name = "Ms. Aisha Mahmoud",      NationalId = "29003031234567", BirthDate = new(1985, 7, 12),  PhoneNumber = "01111111113", Email = "aisha.mahmoud@capital.edu.eg",  Role = "Registrar",       JobTitle = "Senior Registrar Officer",    StructureNodeId = Find("Capital University").Id, PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "ADV-001",  PasswordHash = adminPwd, Name = "Dr. Omar El-Sayed",      NationalId = "27504041234567", BirthDate = new(1982, 9, 28),  PhoneNumber = "01111111114", Email = "omar.elsayed@capital.edu.eg",   Role = "Academic Advisor",JobTitle = "Student Academic Advisor",   StructureNodeId = Find("Nutrition & Food Science").Id, PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "STF-001",  PasswordHash = adminPwd, Name = "Mr. Tamer Said",         NationalId = "29205051234567", BirthDate = new(1990, 1, 15),  PhoneNumber = "01111111115", Email = "tamer.said@capital.edu.eg",     Role = "Staff",           JobTitle = "IT Support Specialist",       StructureNodeId = Find("Capital University").Id, PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "VWR-001",  PasswordHash = adminPwd, Name = "Ms. Nadia Youssef",      NationalId = "29306061234567", BirthDate = new(1992, 4, 8),   PhoneNumber = "01111111116", Email = "nadia.youssef@capital.edu.eg",  Role = "Viewer",          JobTitle = "External Auditor",            StructureNodeId = Find("Capital University").Id, PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
            new() { Id = Guid.NewGuid(), EmployeeCode = "FAC-002",  PasswordHash = adminPwd, Name = "Dr. Ahmed Abdel-Rahman", NationalId = "27807071234567", BirthDate = new(1970, 8, 18),  PhoneNumber = "01111111117", Email = "ahmed.abdelrahman@capital.edu.eg", Role = "Faculty Admin", JobTitle = "Vice Dean for Academic Affairs", StructureNodeId = Find("Mataria").Id, PasswordExpiry = DateTime.UtcNow.AddYears(1), IsActive = true },
        };

        context.Staffs.AddRange(staffList);
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  9. STUDENTS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStudentsAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        var nodes = await context.StructureNodes.ToListAsync();
        StructureNode Find(string name) => nodes.First(n => n.Name.Contains(name));

        var studentPwd = passwordHasher.HashPassword("123456");
        var students = new List<Student>
        {
            new() { Id = Guid.NewGuid(), StudentCode = "20250001", PasswordHash = studentPwd, Name = "Ahmed Mohamed Ali",        NationalId = "30201011234567", BirthDate = new(2002, 1, 1),  PhoneNumber = "01000000001", Email = "ahmed.mohamed@capital.edu.eg",   StructureNodeId = Find("Nutrition & Food Science").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250002", PasswordHash = studentPwd, Name = "Sara Mahmoud Hassan",      NationalId = "30202021234567", BirthDate = new(2002, 2, 2),  PhoneNumber = "01000000002", Email = "sara.hassan@capital.edu.eg",      StructureNodeId = Find("Level 4").Id,  IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250003", PasswordHash = studentPwd, Name = "Mohamed Khaled Ibrahim",   NationalId = "30203031234567", BirthDate = new(2003, 3, 3),  PhoneNumber = "01000000003", Email = "mohamed.ibrahim@capital.edu.eg",  StructureNodeId = Find("Level 4").Id,  IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250004", PasswordHash = studentPwd, Name = "Nourhan Atef El-Sayed",    NationalId = "30204041234567", BirthDate = new(2001, 5, 15), PhoneNumber = "01000000004", Email = "nourhan.atef@capital.edu.eg",     StructureNodeId = Find("Level 1").Id,  IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250005", PasswordHash = studentPwd, Name = "Mariam Tarek Fathy",       NationalId = "30205051234567", BirthDate = new(2001, 8, 22), PhoneNumber = "01000000005", Email = "mariam.tarek@capital.edu.eg",     StructureNodeId = Find("Textile & Clothing").Id,  IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250006", PasswordHash = studentPwd, Name = "Youssef Gamal El-Din",     NationalId = "30206061234567", BirthDate = new(2004, 11, 2), PhoneNumber = "01000000006", Email = "youssef.gamal@capital.edu.eg",    StructureNodeId = Find("Textile & Clothing").Id,  IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250007", PasswordHash = studentPwd, Name = "Omar Hossam El-Din",       NationalId = "30207071234567", BirthDate = new(2003, 4, 10), PhoneNumber = "01000000007", Email = "omar.hossam@capital.edu.eg",      StructureNodeId = Find("Civil Engineering").Id,    IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250008", PasswordHash = studentPwd, Name = "Laila Sherif Kamal",       NationalId = "30208081234567", BirthDate = new(2002, 7, 18), PhoneNumber = "01000000008", Email = "laila.sherif@capital.edu.eg",     StructureNodeId = Find("Civil Engineering").Id,    IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250009", PasswordHash = studentPwd, Name = "Ali Hassan Mohamed",       NationalId = "30209091234567", BirthDate = new(2001, 12, 5), PhoneNumber = "01000000009", Email = "ali.hassan@capital.edu.eg",       StructureNodeId = Find("Computer & Systems Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250010", PasswordHash = studentPwd, Name = "Hagar Mahmoud Ahmed",      NationalId = "30210101234567", BirthDate = new(2003, 9, 14), PhoneNumber = "01000000010", Email = "hagar.mahmoud@capital.edu.eg",    StructureNodeId = Find("Computer & Systems Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250011", PasswordHash = studentPwd, Name = "Karim Mostafa Abdel-Aziz", NationalId = "30211111234567", BirthDate = new(2002, 3, 28), PhoneNumber = "01000000011", Email = "karim.mostafa@capital.edu.eg",    StructureNodeId = Find("Computer & Systems Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250012", PasswordHash = studentPwd, Name = "Salma Adel Naguib",        NationalId = "30212121234567", BirthDate = new(2001, 6, 30), PhoneNumber = "01000000012", Email = "salma.adel@capital.edu.eg",       StructureNodeId = Find("Communications & Information Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250013", PasswordHash = studentPwd, Name = "Hassan Emad El-Din",       NationalId = "30213131234567", BirthDate = new(2004, 1, 8),  PhoneNumber = "01000000013", Email = "hassan.emad@capital.edu.eg",      StructureNodeId = Find("Communications & Information Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250014", PasswordHash = studentPwd, Name = "Dalia Samir Fawzy",        NationalId = "30214141234567", BirthDate = new(2003, 10, 20),PhoneNumber = "01000000014", Email = "dalia.samir@capital.edu.eg",      StructureNodeId = Find("Architectural Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250015", PasswordHash = studentPwd, Name = "Amr Khaled Youssef",       NationalId = "30215151234567", BirthDate = new(2002, 2, 14), PhoneNumber = "01000000015", Email = "amr.khaled@capital.edu.eg",       StructureNodeId = Find("Architectural Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250016", PasswordHash = studentPwd, Name = "Nada Ashraf Ibrahim",      NationalId = "30216161234567", BirthDate = new(2001, 11, 11),PhoneNumber = "01000000016", Email = "nada.ashraf@capital.edu.eg",      StructureNodeId = Find("Biomedical Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
            new() { Id = Guid.NewGuid(), StudentCode = "20250017", PasswordHash = studentPwd, Name = "Mostafa Gamal Hassan",     NationalId = "30217171234567", BirthDate = new(2004, 5, 25), PhoneNumber = "01000000017", Email = "mostafa.gamal@capital.edu.eg",    StructureNodeId = Find("Biomedical Engineering").Id, IsActive = true, PasswordExpiry = DateTime.UtcNow.AddMonths(6) },
        };

        context.Students.AddRange(students);
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    // 10. STAFF ROLE ASSIGNMENTS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStaffRoleAssignmentsAsync(CoreDbContext context)
    {
        var staff = await context.Staffs.ToListAsync();
        var roles = await context.Roles.ToListAsync();
        var nodes = await context.StructureNodes.ToListAsync();

        StructureNode Find(string name) => nodes.First(n => n.Name.Contains(name));
        Staff ByCode(string code) => staff.First(s => s.EmployeeCode == code);
        Role ByName(string name) => roles.First(r => r.Name == name);

        context.StaffRoles.AddRange(
            new StaffRoleAssignment(ByCode("ADMIN-001").Id, ByName("Super Admin").Id, "*", "*")
                { StructureNodeId = Find("Capital University").Id, StructureNodePath = Find("Capital University").Path },
            new StaffRoleAssignment(ByCode("FAC-001").Id, ByName("Faculty Admin").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Home Economics").Id, StructureNodePath = Find("Home Economics").Path },
            new StaffRoleAssignment(ByCode("HOD-001").Id, ByName("Department Head").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Clinical Nutrition").Id, StructureNodePath = Find("Clinical Nutrition").Path },
            new StaffRoleAssignment(ByCode("REG-001").Id, ByName("Registrar").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Capital University").Id, StructureNodePath = Find("Capital University").Path },
            new StaffRoleAssignment(ByCode("ADV-001").Id, ByName("Academic Advisor").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Nutrition & Food Science").Id, StructureNodePath = Find("Nutrition & Food Science").Path },
            new StaffRoleAssignment(ByCode("STF-001").Id, ByName("Staff").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Capital University").Id, StructureNodePath = Find("Capital University").Path },
            new StaffRoleAssignment(ByCode("VWR-001").Id, ByName("Viewer").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Capital University").Id, StructureNodePath = Find("Capital University").Path },
            new StaffRoleAssignment(ByCode("FAC-002").Id, ByName("Faculty Admin").Id, "2025-2026", "Fall")
                { StructureNodeId = Find("Mataria").Id, StructureNodePath = Find("Mataria").Path }
        );

        await context.SaveChangesAsync();
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