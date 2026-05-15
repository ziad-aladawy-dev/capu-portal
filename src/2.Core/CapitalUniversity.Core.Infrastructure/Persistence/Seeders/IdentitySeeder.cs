using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        CoreDbContext context)
    {
        if (context.Students.Any() || context.Staffs.Any())
           return;

        var nutritionProgram = await context.StructureNodes
            .FirstAsync(x =>
                x.Name == "التغذية وعلوم الأطعمة" &&
                x.Type == StructureNodeType.Program);

        var nutritionLevel4 = await context.StructureNodes
            .FirstAsync(x =>
                x.Name == "الرابع" &&
                x.ParentId == nutritionProgram.Id);

        var students = new List<Student>
        {
            new Student
            {
                Id = Guid.NewGuid(),

                StudentCode = "20250001",

                PasswordHash = "123456",

                Name = "أحمد محمد علي",

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

                PasswordHash = "123456",

                Name = "سارة محمود حسن",

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

                PasswordHash = "123456",

                Name = "محمد خالد إبراهيم",

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

                PasswordHash = "123456",

                Name = "دكتور إبراهيم عبد الله",

                NationalId = "27801011234567",

                BirthDate = new DateTime(1978, 1, 1),

                PhoneNumber = "01111111111",

                Email = "ibrahim@capital.edu.eg",

                Role = "Admin",

                JobTitle = "مدير النظام",

                StructureNodeId = nutritionLevel4.Id,

                PasswordExpiry = DateTime.UtcNow.AddMonths(12),

                IsActive = true
            },

            new Staff
            {
                Id = Guid.NewGuid(),

                EmployeeCode = "EMP002",

                PasswordHash = "123456",

                Name = "دكتورة منى أحمد",

                NationalId = "28002021234567",

                BirthDate = new DateTime(1980, 2, 2),

                PhoneNumber = "01111111112",

                Email = "mona@capital.edu.eg",

                Role = "Doctor",

                JobTitle = "أستاذ جامعي",

                StructureNodeId = nutritionProgram.Id,

                PasswordExpiry = DateTime.UtcNow.AddMonths(12),

                IsActive = true
            },

            new Staff
            {
                Id = Guid.NewGuid(),

                EmployeeCode = "EMP003",

                PasswordHash = "123456",

                Name = "محمد سامي",

                NationalId = "28503031234567",

                BirthDate = new DateTime(1985, 3, 3),

                PhoneNumber = "01111111113",

                Email = "samy@capital.edu.eg",

                Role = "Employee",

                JobTitle = "شئون طلاب",

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