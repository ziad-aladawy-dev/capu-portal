////using CapitalUniversity.Core.Domain.AcademicCalendar;
////using CapitalUniversity.Core.Domain.Common;
////using CapitalUniversity.Core.Domain.Identity;
////using CapitalUniversity.Core.Domain.UniversityStructure;
////using CapitalUniversity.Core.Infrastructure.Persistence;
////using Microsoft.EntityFrameworkCore;
////using Microsoft.Extensions.DependencyInjection;
////using Microsoft.Extensions.Logging;
////using System;
////using System.Collections.Generic;
////using System.Linq;
////using System.Threading.Tasks;

////namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders
////{
////    public class DatabaseSeeder
////    {
////        private readonly CoreDbContext _context;
////        private readonly IPasswordHasherService _passwordHasher;
////        private readonly ILogger<DatabaseSeeder> _logger;

////        public DatabaseSeeder(CoreDbContext context, IPasswordHasherService passwordHasher, ILogger<DatabaseSeeder> logger)
////        {
////            _context = context;
////            _passwordHasher = passwordHasher;
////            _logger = logger;
////        }

////        public async Task SeedAsync()
////        {
////            _logger.LogInformation("Starting database seeding...");

////            await SeedUniversities();
////            await SeedFaculties();
////            await SeedFacultySystems();
////            await SeedAcademicPrograms();
////            await SeedLevels();
////            await SeedModules();
////            await SeedServices();
////            await SeedRoles();
////            await SeedRolePermissions();
////            await SeedStaff();
////            await SeedStudents();

////            _logger.LogInformation("Seeding completed.");
////        }

////        private async Task SeedUniversities()
////        {
////            if (!_context.Universities.Any())
////            {
////                var uni = new University
////                {
////                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
////                    NameAr = "جامعة القاهرة",
////                    NameEn = "Cairo University",
////                    Domain = "cu.edu.eg",
////                    LogoUrl = "",
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                await _context.Universities.AddAsync(uni);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded University.");
////            }
////        }

////        private async Task SeedFaculties()
////        {
////            if (!_context.Faculties.Any())
////            {
////                var faculty = new Faculty
////                {
////                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
////                    Code = "FCI",
////                    NameAr = "كلية الحاسبات والمعلومات",
////                    NameEn = "Faculty of Computers and Information",
////                    UniversityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                await _context.Faculties.AddAsync(faculty);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded Faculty.");
////            }
////        }

////        private async Task SeedFacultySystems()
////        {
////            if (!_context.FacultySystems.Any())
////            {
////                var system = new FacultySystem
////                {
////                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
////                    FacultyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
////                    SystemType = SystemTypeEnum.CreditHoursSystem
////                };
////                await _context.FacultySystems.AddAsync(system);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded FacultySystem (Credit Hours).");
////            }
////        }

////        private async Task SeedAcademicPrograms()
////        {
////            if (!_context.AcademicPrograms.Any())
////            {
////                var program = new AcademicProgram
////                {
////                    Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
////                    Code = "CS",
////                    NameAr = "علوم الحاسب",
////                    NameEn = "Computer Science",
////                    FacultySystemId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
////                    ParentId = null,
////                    TotalHours = 132,
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                await _context.AcademicPrograms.AddAsync(program);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded AcademicProgram (CS).");
////            }
////        }

////        private async Task SeedLevels()
////        {
////            if (!_context.Levels.Any())
////            {
////                var level1 = new Level
////                {
////                    Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
////                    Code = "LVL1",
////                    NameAr = "المستوى الأول",
////                    NameEn = "Level 1",
////                    Order = 1,
////                    ProgramId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
////                    TotalHours = 33,
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                var level2 = new Level
////                {
////                    Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
////                    Code = "LVL2",
////                    NameAr = "المستوى الثاني",
////                    NameEn = "Level 2",
////                    Order = 2,
////                    ProgramId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
////                    TotalHours = 33,
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                await _context.Levels.AddRangeAsync(level1, level2);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded Levels.");
////            }
////        }

////        private async Task SeedModules()
////        {
////            if (!_context.Modules.Any())
////            {
////                var modules = new[]
////                {
////                    new Module { Id = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), ModuleKey = "UniversityStructure", DisplayNameAr = "هيكل الجامعة", DisplayNameEn = "University Structure", OrderNumber = 1, CreatedAt = DateTime.UtcNow },
////                    new Module { Id = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"), ModuleKey = "UserManagement", DisplayNameAr = "إدارة المستخدمين", DisplayNameEn = "User Management", OrderNumber = 2, CreatedAt = DateTime.UtcNow },
////                    new Module { Id = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC"), ModuleKey = "AcademicCalendar", DisplayNameAr = "التقويم الأكاديمي", DisplayNameEn = "Academic Calendar", OrderNumber = 3, CreatedAt = DateTime.UtcNow }
////                };
////                await _context.Modules.AddRangeAsync(modules);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded Modules.");
////            }
////        }

////        private async Task SeedServices()
////        {
////            if (!_context.Services.Any())
////            {
////                var services = new[]
////                {
////                    new Service { Id = Guid.Parse("DDDDDDDD-DDDD-DDDD-DDDD-DDDDDDDDDDDD"), ModuleId = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), DisplayNameAr = "عرض الجامعات", DisplayNameEn = "View Universities", OrderNumber = 1, CreatedAt = DateTime.UtcNow },
////                    new Service { Id = Guid.Parse("EEEEEEEE-EEEE-EEEE-EEEE-EEEEEEEEEEEE"), ModuleId = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"), DisplayNameAr = "إدارة الكليات", DisplayNameEn = "Manage Faculties", OrderNumber = 2, CreatedAt = DateTime.UtcNow },
////                    new Service { Id = Guid.Parse("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"), ModuleId = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"), DisplayNameAr = "إدارة الطلاب", DisplayNameEn = "Manage Students", OrderNumber = 1, CreatedAt = DateTime.UtcNow },
////                    new Service { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), ModuleId = Guid.Parse("CCCCCCCC-CCCC-CCCC-CCCC-CCCCCCCCCCCC"), DisplayNameAr = "إدارة السنوات", DisplayNameEn = "Manage Academic Years", OrderNumber = 1, CreatedAt = DateTime.UtcNow }
////                };
////                await _context.Services.AddRangeAsync(services);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded Services.");
////            }
////        }

////        private async Task SeedRoles()
////        {
////            if (!_context.Roles.Any())
////            {
////                var roles = new[]
////                {
////                    new Role { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Name = "SystemAdmin", IsSystemRole = true },
////                    new Role { Id = Guid.Parse("88888888-8888-8888-8888-888888888888"), Name = "Registrar", IsSystemRole = false },
////                    new Role { Id = Guid.Parse("99999999-9999-9999-9999-999999999999"), Name = "Instructor", IsSystemRole = false }
////                };
////                await _context.Roles.AddRangeAsync(roles);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded Roles.");
////            }
////        }

////        private async Task SeedRolePermissions()
////        {
////            if (!_context.RolePermissions.Any())
////            {
////                // Admin has View + Manage (Level 5) on all services
////                var services = await _context.Services.ToListAsync();
////                var adminRoleId = Guid.Parse("77777777-7777-7777-7777-777777777777");
////                foreach (var svc in services)
////                {
////                    var rp = new RolePermission
////                    {
////                        Id = Guid.NewGuid(),
////                        RoleId = adminRoleId,
////                        ServiceId = svc.Id,
////                        Level = 5 // Delete level
////                    };
////                    await _context.RolePermissions.AddAsync(rp);
////                }
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded RolePermissions for Admin.");
////            }
////        }

////        private async Task SeedStaff()
////        {
////            if (!_context.Staffs.Any())
////            {
////                var admin = new Staff
////                {
////                    Id = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-000000000001"),
////                    NationalId = "1111111111",
////                    StaffCode = "ADM001",
////                    NameAr = "مدير النظام",
////                    NameEn = "System Admin",
////                    Email = "admin@cu.edu.eg",
////                    Phone = "01000000000",
////                    UniversityId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
////                    FacultyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
////                    AcademicProgramId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
////                    IsActive = true,
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                admin.PasswordHash = _passwordHasher.HashPassword("Admin@123");
////                await _context.Staffs.AddAsync(admin);
////                await _context.SaveChangesAsync();

////                // Assign Admin Role
////                var staffRole = new StaffRole
////                {
////                    Id = Guid.NewGuid(),
////                    StaffId = admin.Id,
////                    RoleId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
////                    FacultyId = null,
////                    ProgramId = null
////                };
////                await _context.StaffRoles.AddAsync(staffRole);
////                await _context.SaveChangesAsync();

////                _logger.LogInformation("Seeded System Admin staff.");
////            }
////        }

////        private async Task SeedStudents()
////        {
////            if (!_context.Students.Any())
////            {
////                var student = new Student
////                {
////                    Id = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-000000000001"),
////                    NationalId = "2222222222",
////                    StudentCode = "CS240001",
////                    NameAr = "طالب تجريبي",
////                    NameEn = "Test Student",
////                    Email = "student@cu.edu.eg",
////                    Phone = "0111111111",
////                    FacultyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
////                    ProgramId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
////                    LevelId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
////                    CurrentAcademicYearId = null,
////                    CurrentSemesterId = null,
////                    Status = StudentStatusEnum.Active,
////                    EnrollmentDate = DateTime.UtcNow.AddYears(-1),
////                    CreatedAt = DateTime.UtcNow,
////                    IsDeleted = false
////                };
////                student.PasswordHash = _passwordHasher.HashPassword("Student@123");
////                await _context.Students.AddAsync(student);
////                await _context.SaveChangesAsync();
////                _logger.LogInformation("Seeded Test Student.");
////            }
////        }
////    }
////}

//using CapitalUniversity.Core.Domain.Academic;
//using CapitalUniversity.Core.Domain.AcademicCalendar;
//using CapitalUniversity.Core.Domain.Common;
//using CapitalUniversity.Core.Domain.Identity;
//using CapitalUniversity.Core.Domain.UniversityStructure;
//using CapitalUniversity.Core.Infrastructure.Persistence;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Threading.Tasks;

//namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

//public static class DatabaseSeeder
//{
//    public static async Task SeedAsync(IServiceProvider serviceProvider)
//    {
//        using var scope = serviceProvider.CreateScope();
//        var context = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
//        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CoreDbContext>>();

//        await context.Database.MigrateAsync();

//        // 1. Universities
//        if (!await context.Universities.AnyAsync())
//        {
//            logger.LogInformation("Seeding Universities...");
//            var uni = new University
//            {
//                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
//                NameAr = "جامعة القاهرة",
//                NameEn = "Cairo University",
//                Domain = "cu.edu.eg",
//                LogoUrl = "https://cu.edu.eg/logo.png",
//                CreatedAt = DateTime.UtcNow,
//                IsDeleted = false
//            };
//            await context.Universities.AddAsync(uni);
//            await context.SaveChangesAsync();
//        }

//        var university = await context.Universities.FirstAsync();

//        // 2. Faculties
//        if (!await context.Faculties.AnyAsync())
//        {
//            logger.LogInformation("Seeding Faculties...");
//            var faculty1 = new Faculty
//            {
//                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
//                Code = "FCI",
//                NameAr = "كلية الحاسبات والمعلومات",
//                NameEn = "Faculty of Computers and Information",
//                UniversityId = university.Id,
//                CreatedAt = DateTime.UtcNow,
//                IsDeleted = false
//            };
//            await context.Faculties.AddAsync(faculty1);
//            await context.SaveChangesAsync();
//        }

//        var faculty = await context.Faculties.FirstAsync();

//        // 3. FacultySystems
//        if (!await context.FacultySystems.AnyAsync())
//        {
//            logger.LogInformation("Seeding FacultySystems...");
//            var system = new FacultySystem
//            {
//                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
//                FacultyId = faculty.Id,
//                SystemType = SystemTypeEnum.CreditHoursSystem
//            };
//            await context.FacultySystems.AddAsync(system);
//            await context.SaveChangesAsync();
//        }

//        var facultySystem = await context.FacultySystems.FirstAsync();

//        // 4. AcademicPrograms
//        if (!await context.AcademicPrograms.AnyAsync())
//        {
//            logger.LogInformation("Seeding AcademicPrograms...");
//            var program1 = new AcademicProgram
//            {
//                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
//                Code = "CS",
//                NameAr = "علوم الحاسب",
//                NameEn = "Computer Science",
//                FacultySystemId = facultySystem.Id,
//                ParentId = null,
//                TotalHours = 132,
//                CreatedAt = DateTime.UtcNow,
//                IsDeleted = false
//            };
//            await context.AcademicPrograms.AddAsync(program1);
//            await context.SaveChangesAsync();
//        }

//        var program = await context.AcademicPrograms.FirstAsync();

//        // 5. Levels
//        if (!await context.Levels.AnyAsync())
//        {
//            logger.LogInformation("Seeding Levels...");
//            var levels = new[]
//            {
//                new Level { Id = Guid.Parse("55555555-5555-5555-5555-555555555551"), Code = "L1", NameAr = "المستوى الأول", NameEn = "Level 1", Order = 1, ProgramId = program.Id, TotalHours = 33, CreatedAt = DateTime.UtcNow, IsDeleted = false },
//                new Level { Id = Guid.Parse("55555555-5555-5555-5555-555555555552"), Code = "L2", NameAr = "المستوى الثاني", NameEn = "Level 2", Order = 2, ProgramId = program.Id, TotalHours = 33, CreatedAt = DateTime.UtcNow, IsDeleted = false },
//                new Level { Id = Guid.Parse("55555555-5555-5555-5555-555555555553"), Code = "L3", NameAr = "المستوى الثالث", NameEn = "Level 3", Order = 3, ProgramId = program.Id, TotalHours = 33, CreatedAt = DateTime.UtcNow, IsDeleted = false },
//                new Level { Id = Guid.Parse("55555555-5555-5555-5555-555555555554"), Code = "L4", NameAr = "المستوى الرابع", NameEn = "Level 4", Order = 4, ProgramId = program.Id, TotalHours = 33, CreatedAt = DateTime.UtcNow, IsDeleted = false }
//            };
//            await context.Levels.AddRangeAsync(levels);
//            await context.SaveChangesAsync();
//        }

//        var level1 = await context.Levels.OrderBy(l => l.Order).FirstAsync();

//        // 6. Courses (اختياري – يمكنك إضافتها لاحقاً)
//        if (!await context.Courses.AnyAsync())
//        {
//            logger.LogInformation("Seeding Courses...");
//            var course = new Course
//            {
//                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
//                CourseCode = "CS101",
//                NameAr = "مقدمة في علوم الحاسب",
//                NameEn = "Introduction to Computer Science",
//                CreditHours = 3,
//                LevelId = level1.Id,
//            };
//            await context.Courses.AddAsync(course);
//            await context.SaveChangesAsync();
//        }

//        // 7. AcademicYears & Semesters (اختياري – يمكنك إضافة لاحقاً)
//        if (!await context.AcademicYears.AnyAsync())
//        {
//            logger.LogInformation("Seeding AcademicYears...");
//            var year = new AcademicYear
//            {
//                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
//                Name = "2024-2025",
//                StartDate = new DateTime(2024, 9, 1),
//                EndDate = new DateTime(2025, 6, 30),
//                IsCurrent = true
//            };
//            await context.AcademicYears.AddAsync(year);
//            await context.SaveChangesAsync();

//            var semester = new Semester
//            {
//                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
//                AcademicYearId = year.Id,
//                Name = "Fall 2024",
//                StartDate = new DateTime(2024, 9, 1),
//                EndDate = new DateTime(2024, 12, 31),
//                IsCurrent = true
//            };
//            await context.Semesters.AddAsync(semester);
//            await context.SaveChangesAsync();
//        }

//        // 8. Modules & Services
//        if (!await context.Modules.AnyAsync())
//        {
//            logger.LogInformation("Seeding Modules and Services...");
//            var module = new Module
//            {
//                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
//                ModuleKey = "Dashboard",
//                DisplayNameAr = "لوحة التحكم",
//                DisplayNameEn = "Dashboard",
//                OrderNumber = 1,
//                CreatedAt = DateTime.UtcNow,
//            };
//            await context.Modules.AddAsync(module);

//            var module2 = new Module
//            {
//                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2"),
//                ModuleKey = "Students",
//                DisplayNameAr = "الطلاب",
//                DisplayNameEn = "Students",
//                OrderNumber = 2,
//                CreatedAt = DateTime.UtcNow,
//            };
//            await context.Modules.AddAsync(module2);

//            await context.SaveChangesAsync();

//            var services = new[]
//            {
//                new Service { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb11"), ModuleId = module.Id, DisplayNameAr = "رؤية لوحة التحكم", DisplayNameEn = "View Dashboard", OrderNumber = 1, CreatedAt = DateTime.UtcNow },
//                new Service { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12"), ModuleId = module2.Id, DisplayNameAr = "إدارة الطلاب", DisplayNameEn = "Manage Students", OrderNumber = 1, CreatedAt = DateTime.UtcNow },
//                new Service { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb13"), ModuleId = module2.Id, DisplayNameAr = "عرض الطلاب", DisplayNameEn = "View Students", OrderNumber = 2, CreatedAt = DateTime.UtcNow }
//            };
//            await context.Services.AddRangeAsync(services);
//            await context.SaveChangesAsync();
//        }

//        // 9. Roles
//        if (!await context.Roles.AnyAsync())
//        {
//            logger.LogInformation("Seeding Roles...");
//            var roles = new[]
//            {
//                new Role { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"), Name = "SuperAdmin", IsSystemRole = true },
//                new Role { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2"), Name = "Admin", IsSystemRole = false },
//                new Role { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3"), Name = "Staff", IsSystemRole = false },
//                new Role { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc4"), Name = "Instructor", IsSystemRole = false }
//            };
//            await context.Roles.AddRangeAsync(roles);
//            await context.SaveChangesAsync();
//        }

//        // 10. RolePermissions (Assign permissions to roles)
//        if (!await context.RolePermissions.AnyAsync())
//        {
//            logger.LogInformation("Seeding RolePermissions...");
//            var superAdminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
//            var adminRole = await context.Roles.FirstAsync(r => r.Name == "Admin");
//            var staffRole = await context.Roles.FirstAsync(r => r.Name == "Staff");
//            var services = await context.Services.ToListAsync();

//            // SuperAdmin gets level 5 on all services
//            foreach (var service in services)
//            {
//                var rp = new RolePermission
//                {
//                    Id = Guid.NewGuid(),
//                    RoleId = superAdminRole.Id,
//                    ServiceId = service.Id,
//                    Level = 5
//                };
//                await context.RolePermissions.AddAsync(rp);
//            }

//            // Admin gets level 4 on Students service and level 5 on Dashboard
//            var dashboardService = services.FirstOrDefault(s => s.Module.ModuleKey == "Dashboard");
//            var studentsService = services.FirstOrDefault(s => s.Module.ModuleKey == "Students" && s.DisplayNameEn == "Manage Students");
//            if (dashboardService != null)
//            {
//                await context.RolePermissions.AddAsync(new RolePermission
//                {
//                    Id = Guid.NewGuid(),
//                    RoleId = adminRole.Id,
//                    ServiceId = dashboardService.Id,
//                    Level = 5
//                });
//            }
//            if (studentsService != null)
//            {
//                await context.RolePermissions.AddAsync(new RolePermission
//                {
//                    Id = Guid.NewGuid(),
//                    RoleId = adminRole.Id,
//                    ServiceId = studentsService.Id,
//                    Level = 4
//                });
//            }

//            await context.SaveChangesAsync();
//        }

//        // 11. Staff (Admin user)
//        if (!await context.Staffs.AnyAsync())
//        {
//            logger.LogInformation("Seeding Staff (Admin)...");
//            var hasher = new PasswordHasher<object>();
//            var adminStaff = new Staff
//            {
//                Id = Guid.Parse("dddddddd-dddd-dddd-dddd-ddddddddddd1"),
//                NationalId = "12345678901234",
//                StaffCode = "ADMIN001",
//                NameAr = "مدير النظام",
//                NameEn = "System Admin",
//                Email = "admin@cu.edu.eg",
//                Phone = "01000000000",
//                UniversityId = university.Id,
//                FacultyId = faculty.Id,
//                IsActive = true,
//                CreatedAt = DateTime.UtcNow,
//                IsDeleted = false
//            };
//            adminStaff.PasswordHash = hasher.HashPassword(null, "Admin@123");
//            await context.Staffs.AddAsync(adminStaff);
//            await context.SaveChangesAsync();

//            // Assign SuperAdmin role to this staff
//            var superAdminRole = await context.Roles.FirstAsync(r => r.Name == "SuperAdmin");
//            var staffRoleEntity = new StaffRole
//            {
//                Id = Guid.NewGuid(),
//                StaffId = adminStaff.Id,
//                RoleId = superAdminRole.Id,
//                FacultyId = null,
//                ProgramId = null
//            };
//            await context.StaffRoles.AddAsync(staffRoleEntity);
//            await context.SaveChangesAsync();
//        }

//        // 12. Student
//        if (!await context.Students.AnyAsync())
//        {
//            logger.LogInformation("Seeding Student...");
//            var hasher = new PasswordHasher<object>();
//            var student = new Student
//            {
//                Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1"),
//                NationalId = "29810101234567",
//                StudentCode = "STU240001",
//                NameAr = "أحمد محمد",
//                NameEn = "Ahmed Mohamed",
//                Email = "ahmed@student.cu.edu.eg",
//                Phone = "01111111111",
//                FacultyId = faculty.Id,
//                ProgramId = program.Id,
//                LevelId = level1.Id,
//                Status = StudentStatusEnum.Active,
//                EnrollmentDate = DateTime.UtcNow,
//                CreatedAt = DateTime.UtcNow,
//                IsDeleted = false
//            };
//            student.PasswordHash = hasher.HashPassword(null, student.NationalId); // default password = NationalId
//            await context.Students.AddAsync(student);
//            await context.SaveChangesAsync();
//        }

//        logger.LogInformation("Seeding completed successfully.");
//    }
//}