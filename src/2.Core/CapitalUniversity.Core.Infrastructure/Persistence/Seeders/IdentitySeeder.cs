using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class IdentitySeeder
{
    public static async Task SeedUsersAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        if (await context.Students.AnyAsync() || await context.Staffs.AnyAsync())
            return;

        var hashedPassword = passwordHasher.HashPassword("123456");

        var allNodes = await context.StructureNodes.ToListAsync();

        StructureNode? FindProgram(string arabicName) =>
            allNodes.FirstOrDefault(n => n.Type == StructureNodeType.Program && ExtractArabicName(n.Name) == arabicName);

        StructureNode? FindLevelUnderProgram(string levelArabicName, Guid programId) =>
            allNodes.FirstOrDefault(n => n.Type == StructureNodeType.Level && n.ParentId == programId && ExtractArabicName(n.Name) == levelArabicName);

        StructureNode? FindFaculty(string arabicName) =>
            allNodes.FirstOrDefault(n => n.Type == StructureNodeType.Faculty && ExtractArabicName(n.Name) == arabicName);

        StructureNode? FindUniversity() =>
            allNodes.FirstOrDefault(n => n.Type == StructureNodeType.University);

        string ExtractArabicName(string nameJson)
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(nameJson);
                return dict?.GetValueOrDefault("ar") ?? string.Empty;
            }
            catch
            {
                return nameJson;
            }
        }

        var csProgram = FindProgram("علوم الحاسب");
        var isProgram = FindProgram("نظم المعلومات");
        var civilProgram = FindProgram("الهندسة المدنية");
        var accountingProgram = FindProgram("المحاسبة");
        var marketingProgram = FindProgram("التسويق");
        var aiProgram = FindProgram("الذكاء الاصطناعي");
        var electricalProgram = FindProgram("الهندسة الكهربائية");

        var csLevel1 = csProgram != null ? FindLevelUnderProgram("المستوى الأول", csProgram.Id) : null;
        var csLevel3 = csProgram != null ? FindLevelUnderProgram("المستوى الثالث", csProgram.Id) : null;
        var csLevel4 = csProgram != null ? FindLevelUnderProgram("المستوى الرابع", csProgram.Id) : null;

        var isLevel1 = isProgram != null ? FindLevelUnderProgram("المستوى الأول", isProgram.Id) : null;

        var civilLevel1 = civilProgram != null ? FindLevelUnderProgram("الفرقة الأولى", civilProgram.Id) : null;

        var accountingLevel1 = accountingProgram != null ? FindLevelUnderProgram("المستوى الأول", accountingProgram.Id) : null;

        var students = new List<Student>();

        if (csLevel1 != null)
        {
            students.Add(new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "CS2024001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "أحمد محمد" }, { "en", "Ahmed Mohamed" } }),
                NationalId = "29901011234567",
                BirthDate = new DateTime(2000, 1, 1),
                PhoneNumber = "01000000001",
                Email = "ahmed.cs@university.edu",
                StructureNodeId = csLevel1.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (csLevel3 != null)
        {
            students.Add(new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "CS2024002",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "سارة أحمد" }, { "en", "Sara Ahmed" } }),
                NationalId = "29902021234567",
                BirthDate = new DateTime(2001, 2, 2),
                PhoneNumber = "01000000002",
                Email = "sara.cs@university.edu",
                StructureNodeId = csLevel3.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (csLevel4 != null)
        {
            students.Add(new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "CS2024003",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "محمد خالد" }, { "en", "Mohamed Khaled" } }),
                NationalId = "29903031234567",
                BirthDate = new DateTime(2002, 3, 3),
                PhoneNumber = "01000000003",
                Email = "mohamed.cs@university.edu",
                StructureNodeId = csLevel4.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (isLevel1 != null)
        {
            students.Add(new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "IS2024001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "نور علي" }, { "en", "Noor Ali" } }),
                NationalId = "29904041234567",
                BirthDate = new DateTime(2000, 4, 4),
                PhoneNumber = "01000000004",
                Email = "noor.is@university.edu",
                StructureNodeId = isLevel1.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (civilLevel1 != null)
        {
            students.Add(new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "CV2024001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "كريم سامي" }, { "en", "Kareem Samy" } }),
                NationalId = "29905051234567",
                BirthDate = new DateTime(2001, 5, 5),
                PhoneNumber = "01000000005",
                Email = "kareem.civil@university.edu",
                StructureNodeId = civilLevel1.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (accountingLevel1 != null)
        {
            students.Add(new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "ACC2024001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "منى محمود" }, { "en", "Mona Mahmoud" } }),
                NationalId = "29906061234567",
                BirthDate = new DateTime(2002, 6, 6),
                PhoneNumber = "01000000006",
                Email = "mona.acc@university.edu",
                StructureNodeId = accountingLevel1.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (students.Any())
        {
            await context.Students.AddRangeAsync(students);
            await context.SaveChangesAsync();
            Console.WriteLine($"✅ تمت إضافة {students.Count} طالب.");
        }
        else
        {
            Console.WriteLine("⚠️ لم يتم العثور على أي عقدة مناسبة للطلاب. تأكد من تشغيل UniversityStructureSeeder أولاً.");
        }

        var universityNode = FindUniversity();
        var engineeringFaculty = FindFaculty("كلية الهندسة");
        var csProgramNode = csProgram;

        var staff = new List<Staff>();

        if (universityNode != null)
        {
            staff.Add(new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "ADMIN001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "إبراهيم عبد الله" }, { "en", "Ibrahim Abdullah" } }),
                NationalId = "27801011234567",
                BirthDate = new DateTime(1978, 1, 1),
                PhoneNumber = "01111111111",
                Email = "ibrahim@capital.edu.eg",
                Role = "SuperAdmin",
                JobTitle = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "مدير النظام" }, { "en", "System Administrator" } }),
                StructureNodeId = universityNode.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(12),
                IsActive = true,
                SessionVersion = 0
            });
        }

        var staff2Node = csProgramNode?.Id ?? engineeringFaculty?.Id ?? universityNode?.Id;
        if (staff2Node.HasValue)
        {
            staff.Add(new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "PROF001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "أحمد على" }, { "en", "Ahmed Ali" } }),
                NationalId = "27802021234567",
                BirthDate = new DateTime(1975, 6, 15),
                PhoneNumber = "01111111112",
                Email = "ahmed.ali@university.edu",
                Role = "Professor",
                JobTitle = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "أستاذ جامعي" }, { "en", "University Professor" } }),
                StructureNodeId = staff2Node.Value,
                PasswordExpiry = DateTime.UtcNow.AddMonths(12),
                IsActive = true,
                SessionVersion = 0
            });
        }

        var staff3Node = engineeringFaculty?.Id ?? universityNode?.Id;
        if (staff3Node.HasValue)
        {
            staff.Add(new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "EMP003",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "محمد سامي" }, { "en", "Mohamed Samy" } }),
                NationalId = "28503031234567",
                BirthDate = new DateTime(1985, 3, 3),
                PhoneNumber = "01111111113",
                Email = "samy@capital.edu.eg",
                Role = "Employee",
                JobTitle = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "شؤون طلاب" }, { "en", "Student Affairs" } }),
                StructureNodeId = staff3Node.Value,
                PasswordExpiry = DateTime.UtcNow.AddMonths(12),
                IsActive = true,
                SessionVersion = 0
            });
        }

        if (staff.Any())
        {
            await context.Staffs.AddRangeAsync(staff);
            await context.SaveChangesAsync();
        }
    }

    public static async Task<Role> EnsureSuperAdminRoleAsync(CoreDbContext context)
    {
        var role = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Super Admin");
        if (role == null)
        {
            role = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Super Admin",
                IsSystemRole = true
            };
            context.Roles.Add(role);
            await context.SaveChangesAsync();
        }
        return role;
    }

    public static async Task AssignUserToSuperAdminRoleAsync(CoreDbContext context, string userEmail)
    {
        var user = await context.Staffs.FirstOrDefaultAsync(s => s.Email == userEmail);
        if (user == null) return;

        var superAdminRole = await EnsureSuperAdminRoleAsync(context);
        var existing = await context.StaffRoles
            .FirstOrDefaultAsync(sr => sr.StaffId == user.Id && sr.RoleId == superAdminRole.Id);
        if (existing == null)
        {
            context.StaffRoles.Add(new StaffRoleAssignment(user.Id, superAdminRole.Id, "Global", "Global")
            {
                StructureNodeId = null,
                StructureNodePath = null
            });
            user.SessionVersion++;
            await context.SaveChangesAsync();
        }
    }

    public static async Task GrantAllPermissionsToSuperAdminAsync(CoreDbContext context)
    {
        var superAdminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Super Admin");
        if (superAdminRole == null) return;

        var existingPermissions = context.RolePermissions.Where(rp => rp.RoleId == superAdminRole.Id);
        context.RolePermissions.RemoveRange(existingPermissions);

        var allResources = await context.Resources.ToListAsync();
        var actions = new[] { "View", "Insert", "EditClose", "Open", "Delete" };

        foreach (var resource in allResources)
        {
            foreach (var action in actions)
            {
                context.RolePermissions.Add(new RolePermission(superAdminRole.Id, resource.Id, action));
            }
        }

        await context.SaveChangesAsync();
    }

    public static async Task CompleteSeedingAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        await SeedUsersAsync(context, passwordHasher);
        await EnsureSuperAdminRoleAsync(context);
        await AssignUserToSuperAdminRoleAsync(context, "ibrahim@capital.edu.eg");
        await GrantAllPermissionsToSuperAdminAsync(context);
    }
}