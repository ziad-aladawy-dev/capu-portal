using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class IdentitySeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        if (await context.Students.AnyAsync() || await context.Staffs.AnyAsync())
            return;

        var allPrograms = await context.StructureNodes
            .Where(x => x.Type == StructureNodeType.Program)
            .ToListAsync();

        var nutritionProgram = allPrograms.FirstOrDefault(x =>
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(x.Name);
                return dict?["ar"] == "التغذية وعلوم الأطعمة";
            }
            catch { return false; }
        });

        if (nutritionProgram == null)
            throw new Exception("Nutrition program not found in seeder.");

        var allLevels = await context.StructureNodes
            .Where(x => x.Type == StructureNodeType.Level)
            .ToListAsync();

        var nutritionLevel4 = allLevels.FirstOrDefault(x =>
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(x.Name);
                return dict?["ar"] == "الرابع" && x.ParentId == nutritionProgram.Id;
            }
            catch { return false; }
        });

        if (nutritionLevel4 == null)
            throw new Exception("Level 4 not found under nutrition program.");

        var hashedPassword = passwordHasher.HashPassword("123456");

        var students = new List<Student>
        {
            new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "20250001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "أحمد محمد علي" }, { "en", "Ahmed Mohamed Ali" } }),
                NationalId = "30201011234567",
                BirthDate = new DateTime(2002, 1, 1),
                PhoneNumber = "01000000001",
                Email = "ahmed@capital.edu.eg",
                StructureNodeId = nutritionProgram.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true
            },
            new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "20250002",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "سارة محمود حسن" }, { "en", "Sara Mahmoud Hassan" } }),
                NationalId = "30202021234567",
                BirthDate = new DateTime(2002, 2, 2),
                PhoneNumber = "01000000002",
                Email = "sara@capital.edu.eg",
                StructureNodeId = nutritionLevel4.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true
            },
            new Student
            {
                Id = Guid.NewGuid(),
                StudentCode = "20250003",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "محمد خالد إبراهيم" }, { "en", "Mohamed Khaled Ibrahim" } }),
                NationalId = "30203031234567",
                BirthDate = new DateTime(2003, 3, 3),
                PhoneNumber = "01000000003",
                Email = "mohamed@capital.edu.eg",
                StructureNodeId = nutritionLevel4.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(6),
                IsActive = true
            }
        };

        var staff = new List<Staff>
        {
            new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "EMP001",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "دكتور إبراهيم عبد الله" }, { "en", "Dr. Ibrahim Abdullah" } }),
                NationalId = "27801011234567",
                BirthDate = new DateTime(1978, 1, 1),
                PhoneNumber = "01111111111",
                Email = "ibrahim@capital.edu.eg",
                Role = "Admin",
                JobTitle = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "مدير النظام" }, { "en", "System Administrator" } }),
                StructureNodeId = nutritionLevel4.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(12),
                IsActive = true
            },
            new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = "EMP002",
                PasswordHash = hashedPassword,
                Name = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "دكتورة منى أحمد" }, { "en", "Dr. Mona Ahmed" } }),
                NationalId = "28002021234567",
                BirthDate = new DateTime(1980, 2, 2),
                PhoneNumber = "01111111112",
                Email = "mona@capital.edu.eg",
                Role = "Doctor",
                JobTitle = JsonSerializer.Serialize(new Dictionary<string, string> { { "ar", "أستاذ جامعي" }, { "en", "University Professor" } }),
                StructureNodeId = nutritionProgram.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(12),
                IsActive = true
            },
            new Staff
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
                StructureNodeId = nutritionLevel4.Id,
                PasswordExpiry = DateTime.UtcNow.AddMonths(12),
                IsActive = true
            }
        };

        await context.Students.AddRangeAsync(students);
        await context.Staffs.AddRangeAsync(staff);
        await context.SaveChangesAsync();
    }
}