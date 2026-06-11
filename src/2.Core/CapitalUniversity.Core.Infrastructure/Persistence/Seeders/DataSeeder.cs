using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Infrastructure.Services.Authorization.Manifest;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Seeders;

public static class DataSeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher, ManifestActionExpander actionExpander)
    {
        // Tables that are "one-shot" (skip if any rows exist).
        //
        // StructureNodes gates on THIS seeder's own root, not AnyAsync: the
        // bilingual demo tree (UniversityStructureSeeder, runs first) already
        // occupies the table on a fresh database, which used to skip this real
        // tree and starve every downstream section (plans, students, offerings).
        if (await context.StructureNodes.AnyAsync(n => n.ParentId == null && n.Name == "Capital University"))
            Console.WriteLine("[Seed] StructureNodes: already populated, skipping.");
        else
            await RunStepAsync("StructureNodes", () => SeedStructureAsync(context));
        await RunOnceAsync("AcademicYears",   context.AcademicYears,   () => SeedAcademicTimelineAsync(context));
        await RunOnceAsync("Roles", context.Roles, () => SeedRolesAsync(context));

        // Idempotent / self-healing: always run, upsert by natural key so stale state converges.
        await RunStepAsync("Modules",   () => SeedAuthModulesAsync(context));
        await RunStepAsync("Resources", () => SeedAuthResourcesAsync(context));
        await RunStepAsync("RolePermissions", () => SeedRolePermissionsAsync(context, actionExpander));
        await RunStepAsync("Staffs",          () => SeedStaffAsync(context, passwordHasher));
        await RunStepAsync("Students",        () => SeedStudentsAsync(context, passwordHasher));
        await RunStepAsync("StaffRoles",      () => SeedStaffRoleAssignmentsAsync(context));
        await RunOnceAsync("StaffPermissions", context.StaffPermissions, () => SeedStaffPermissionOverridesAsync(context));
        await RunOnceAsync("Notifications",   context.Notifications,   () => SeedNotificationsAsync(context));
        await RunStepAsync("Courses", () => SeedCoursesAsync(context));
        // Per-plan idempotent (skip-by-name) rather than one-shot: a partial run
        // (e.g. structure tree missing for some programs) must converge on the
        // next boot instead of being frozen by an AnyAsync guard.
        await RunStepAsync("AcademicPlans", () => SeedAcademicPlansAsync(context));
    }

    /// <summary>
    /// Seeds the full course catalog with upsert-by-code idempotency so new
    /// courses can be added on subsequent seed runs without duplicating existing
    /// rows. Bilingual titles per the localization convention.
    /// </summary>
    private static async Task SeedCoursesAsync(CoreDbContext context)
    {
        var seedCourses = new (string Code, string TitleAr, string TitleEn, int CreditHours, CourseCategory Category)[]
        {
            // ── Computer Science & Engineering ──
            ("CS101",   "مقدمة في علوم الحاسب",          "Introduction to Computer Science", 3, CourseCategory.ProgramRequirement),
            ("CS102",   "برمجة حاسبات (2)",               "Computer Programming II",          3, CourseCategory.ProgramRequirement),
            ("CS201",   "هياكل البيانات والخوارزميات",   "Data Structures and Algorithms",   3, CourseCategory.ProgramRequirement),
            ("CS202",   "تصميم قواعد البيانات",           "Database Design",                  3, CourseCategory.ProgramRequirement),
            ("CS301",   "هندسة البرمجيات",                "Software Engineering",             3, CourseCategory.ProgramRequirement),
            ("CS401",   "التعلم الآلي",                   "Machine Learning",                 3, CourseCategory.ProgramRequirement),
            ("CS402",   "تأمين المعلومات",                "Information Security",             3, CourseCategory.ProgramRequirement),
            ("CS-ELC1", "موضوعات في الذكاء الاصطناعي",   "Topics in AI",                     3, CourseCategory.Elective),

            // ── Mathematics ──
            ("MATH101", "تفاضل وتكامل (1)",              "Calculus I",                       4, CourseCategory.FacultyRequirement),
            ("MATH102", "تفاضل وتكامل (2)",              "Calculus II",                      4, CourseCategory.FacultyRequirement),
            ("MATH201", "المعادلات التفاضلية",           "Differential Equations",           3, CourseCategory.FacultyRequirement),
            ("MATH301", "الاحتمالات والإحصاء",           "Probability & Statistics",         3, CourseCategory.FacultyRequirement),

            // ── Sciences ──
            ("PHYS101", "الفيزياء (1)",                  "Physics I",                        4, CourseCategory.FacultyRequirement),
            ("PHYS102", "الفيزياء (2)",                  "Physics II",                       4, CourseCategory.FacultyRequirement),
            ("CHEM101", "الكيمياء العامة",               "General Chemistry",                3, CourseCategory.FacultyRequirement),

            // ── Civil Engineering ──
            ("CE201",   "ميكانيكا الهندسة",              "Engineering Mechanics",            3, CourseCategory.ProgramRequirement),
            ("CE202",   "مقاومة المواد",                 "Strength of Materials",            3, CourseCategory.ProgramRequirement),
            ("CE301",   "تحليل الإنشاءات",               "Structural Analysis",              3, CourseCategory.ProgramRequirement),
            ("CE302",   "هندسة الأساسات",                "Foundation Engineering",           3, CourseCategory.ProgramRequirement),
            ("CE401",   "هندسة النقل",                   "Transportation Engineering",       3, CourseCategory.ProgramRequirement),

            // ── Architectural Engineering ──
            ("ARCH201", "تاريخ العمارة",                 "History of Architecture",          2, CourseCategory.ProgramRequirement),
            ("ARCH202", "الرسم المعماري",                "Architectural Drawing",            3, CourseCategory.ProgramRequirement),
            ("ARCH301", "التصميم المعماري (1)",          "Architectural Design I",           4, CourseCategory.ProgramRequirement),
            ("ARCH401", "التخطيط العمراني",              "Urban Planning",                   3, CourseCategory.ProgramRequirement),

            // ── Mechanical Engineering ──
            ("ME201",   "الديناميكا الحرارية",           "Thermodynamics",                   3, CourseCategory.ProgramRequirement),
            ("ME202",   "ميكانيكا الموائع",              "Fluid Mechanics",                  3, CourseCategory.ProgramRequirement),
            ("ME301",   "انتقال الحرارة",                "Heat Transfer",                    3, CourseCategory.ProgramRequirement),
            ("ME302",   "هندسة التصنيع",                 "Manufacturing Engineering",        3, CourseCategory.ProgramRequirement),

            // ── Electrical Engineering ──
            ("EE201",   "الدوائر الكهربائية",            "Electric Circuits",                3, CourseCategory.ProgramRequirement),
            ("EE202",   "الإلكترونيات",                  "Electronics",                      3, CourseCategory.ProgramRequirement),
            ("EE301",   "أنظمة التحكم",                  "Control Systems",                  3, CourseCategory.ProgramRequirement),
            ("EE302",   "الاتصالات التناظرية",           "Analog Communications",            3, CourseCategory.ProgramRequirement),

            // ── Home Economics / Nutrition ──
            ("HE101",   "التغذية الأساسية",              "Basic Nutrition",                  3, CourseCategory.ProgramRequirement),
            ("HE102",   "علوم الأطعمة",                  "Food Science",                     3, CourseCategory.ProgramRequirement),
            ("HE201",   "التغذية العلاجية",              "Clinical Nutrition",               3, CourseCategory.ProgramRequirement),
            ("HE202",   "إدارة المؤسسات الغذائية",       "Food Service Management",          3, CourseCategory.ProgramRequirement),
            ("HE301",   "التغذية المجتمعية",             "Community Nutrition",              3, CourseCategory.ProgramRequirement),

            // ── Textiles ──
            ("TEX101",  "المنسوجات والملابس",            "Textiles & Clothing",              3, CourseCategory.ProgramRequirement),
            ("TEX201",  "تصميم الأزياء",                 "Fashion Design",                   3, CourseCategory.ProgramRequirement),
            ("TEX301",  "تكنولوجيا الملابس",             "Apparel Technology",               3, CourseCategory.ProgramRequirement),

            // ── Child & Family ──
            ("CHLD101", "تنمية الطفل",                   "Child Development",                3, CourseCategory.ProgramRequirement),
            ("CHLD201", "إدارة رياض الأطفال",            "Kindergarten Management",          3, CourseCategory.ProgramRequirement),

            // ── Biomedical Engineering ──
            ("BME201",  "الموائع الحيوية",               "Biofluid Mechanics",               3, CourseCategory.ProgramRequirement),
            ("BME202",  "الإشارات الحيوية",              "Biosignals",                       3, CourseCategory.ProgramRequirement),
            ("BME301",  "الأجهزة الطبية",                "Medical Devices",                  3, CourseCategory.ProgramRequirement),

            // ── General / University Requirements ──
            ("UNIV100", "التفكير الناقد",                 "Critical Thinking",                2, CourseCategory.UniversityRequirement),
            ("GEN101",  "مهارات الحاسب",                 "Computer Skills",                  2, CourseCategory.GeneralEducation),
            ("GEN102",  "اللغة العربية",                 "Arabic Language",                  2, CourseCategory.UniversityRequirement),
            ("GEN103",  "اللغة الإنجليزية",              "English Language",                 2, CourseCategory.UniversityRequirement),
            ("GEN150",  "مهارات الإلقاء",                 "Public Speaking",                  2, CourseCategory.GeneralEducation),
            ("GEN201",  "تاريخ مصر الحديث",              "Modern Egyptian History",          2, CourseCategory.GeneralEducation),
            ("GEN202",  "التربية البيئية",               "Environmental Education",          2, CourseCategory.GeneralEducation),
        };

        var existingCodes = await context.Courses.Select(c => c.Code).ToHashSetAsync();
        var added = 0;

        foreach (var (code, titleAr, titleEn, hours, category) in seedCourses)
        {
            if (existingCodes.Contains(code)) continue;
            context.Courses.Add(new Course
            {
                Id = Guid.NewGuid(),
                Code = code,
                Title = LocalizedJson.Of(titleAr, titleEn),
                CreditHours = hours,
                Category = category,
                IsActive = true,
            });
            added++;
        }

        if (added > 0)
            await context.SaveChangesAsync();
        Console.WriteLine($"[Seed] Courses: +{added} added (catalog now has {existingCodes.Count + added} courses).");
    }

    /// <summary>
    /// Seeds academic plans for each program with course-to-level-semester mappings.
    /// One-shot — skipped if any plans exist. Plans reference the StructureNode
    /// (program) and Course tables, so this runs after both are populated.
    /// Plan names and course IDs are cross-referenced at seed time so data is
    /// always consistent with the current catalog and structure.
    /// </summary>
    private static async Task SeedAcademicPlansAsync(CoreDbContext context)
    {
        var nodes = await context.StructureNodes.ToListAsync();
        var courses = await context.Courses.ToDictionaryAsync(c => c.Code);
        var years = await context.AcademicYears.OrderBy(y => y.Name).ToListAsync();
        var effectiveFrom = years.FirstOrDefault()?.StartDate ?? new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var programs = nodes.Where(n => n.Type == StructureNodeType.Program).ToList();

        // Exact name first: the demo tree stores bilingual JSON names whose EN
        // half can substring-match (e.g. "Civil Engineering" exists in both trees).
        StructureNode? GetProgram(string name) =>
            programs.FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? programs.FirstOrDefault(n => n.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

        var existingPlanNames = (await context.AcademicPlans.Select(p => p.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Guid? MakePlan(string name, string programName, (string Code, int Level, int Semester, bool Mandatory)[] planCourses)
        {
            if (existingPlanNames.Contains(name)) return null;

            var prog = GetProgram(programName);
            if (prog == null)
            {
                Console.WriteLine($"[Seed] AcademicPlans: program '{programName}' not found — skipping plan '{name}'.");
                return null;
            }

            var plan = new AcademicPlan
            {
                Id = Guid.NewGuid(),
                StructureNodeId = prog.Id,
                Name = name,
                EffectiveFrom = effectiveFrom,
                IsActive = true,
            };

            foreach (var (code, level, sem, mandatory) in planCourses)
            {
                if (!courses.TryGetValue(code, out var course))
                {
                    Console.WriteLine($"[Seed] AcademicPlans: plan '{name}' — course '{code}' not found, skipping.");
                    continue;
                }
                plan.PlanCourses.Add(new AcademicPlanCourse
                {
                    Id = Guid.NewGuid(),
                    AcademicPlanId = plan.Id,
                    CourseId = course.Id,
                    Level = level,
                    Semester = sem,
                    IsMandatory = mandatory,
                });
            }

            context.AcademicPlans.Add(plan);
            return plan.Id;
        }

        // ── Faculty of Engineering – Helwan (Computer & Systems) ──
        MakePlan("BSc Computer & Systems Engineering 2025",
            "Computer & Systems Engineering", new[]
            {
                ("CS101", 1, 1, true), ("MATH101", 1, 1, true), ("PHYS101", 1, 1, true), ("GEN101", 1, 1, true), ("UNIV100", 1, 1, true),
                ("CS102", 1, 2, true), ("MATH102", 1, 2, true), ("PHYS102", 1, 2, true), ("GEN102", 1, 2, true), ("GEN150",  1, 2, true),
                ("CS201", 2, 1, true), ("MATH201", 2, 1, true), ("EE201",   2, 1, true), ("GEN103", 2, 1, true), ("GEN201",  2, 1, false),
                ("CS202", 2, 2, true), ("MATH301", 2, 2, true), ("EE202",   2, 2, true), ("GEN202", 2, 2, false), ("CS-ELC1", 2, 2, false),
                ("CS301", 3, 1, true), ("CE201",   3, 1, false), ("ME201",  3, 1, false), ("EE301",  3, 1, false),
                ("CS401", 3, 2, true), ("CS402",   3, 2, true),
                ("CE301", 4, 1, false), ("CE401",  4, 1, false), ("BME201", 4, 1, false),
            });

        // ── Faculty of Engineering – Helwan (Communications) ──
        MakePlan("BSc Communications Engineering 2025",
            "Communications & Information Engineering", new[]
            {
                ("MATH101", 1, 1, true), ("PHYS101",1, 1, true), ("CS101",  1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("MATH102", 1, 2, true), ("PHYS102",1, 2, true), ("CS102",  1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("MATH201", 2, 1, true), ("EE201",  2, 1, true), ("GEN103", 2, 1, true),
                ("EE202",   2, 2, true), ("MATH301",2, 2, true), ("GEN201", 2, 2, false),
                ("EE301",   3, 1, true), ("EE302",  3, 1, true), ("GEN202", 3, 1, false),
                ("CS402",   4, 1, true), ("CS301",  4, 1, true),
            });

        // ── Faculty of Engineering – Helwan (Biomedical) ──
        MakePlan("BSc Biomedical Engineering 2025",
            "Biomedical Engineering", new[]
            {
                ("MATH101", 1, 1, true), ("PHYS101",1, 1, true), ("CHEM101",1, 1, true), ("GEN101", 1, 1, true), ("UNIV100", 1, 1, true),
                ("MATH102", 1, 2, true), ("PHYS102",1, 2, true), ("GEN102", 1, 2, true), ("GEN150",  1, 2, true),
                ("MATH201", 2, 1, true), ("BME201", 2, 1, true), ("EE201",  2, 1, true), ("GEN103",  2, 1, true),
                ("BME202", 2, 2, true), ("MATH301",2, 2, true), ("EE202",  2, 2, true), ("GEN201",  2, 2, false),
                ("BME301", 3, 1, true), ("ME201",  3, 1, true), ("GEN202", 3, 1, false),
                ("CS401",  4, 1, false),
            });

        // ── Faculty of Engineering – Mataria (Civil) ──
        MakePlan("BSc Civil Engineering 2025",
            "Civil Engineering", new[]
            {
                ("MATH101", 1, 1, true), ("PHYS101", 1, 1, true), ("GEN101", 1, 1, true), ("UNIV100", 1, 1, true), ("CHEM101", 1, 1, true),
                ("MATH102", 1, 2, true), ("PHYS102", 1, 2, true), ("GEN102", 1, 2, true), ("GEN150",  1, 2, true),
                ("MATH201", 2, 1, true), ("CE201",   2, 1, true), ("CS101",  2, 1, false), ("GEN103",  2, 1, true),
                ("CE202",   2, 2, true), ("MATH301", 2, 2, true), ("GEN201", 2, 2, false), ("GEN202",  2, 2, false),
                ("CE301",   3, 1, true), ("ME201",   3, 1, true), ("EE201",  3, 1, false),
                ("CE302",   3, 2, true), ("ME202",   3, 2, false),
                ("CE401",   4, 1, true), ("ARCH201", 4, 1, false),
            });

        // ── Faculty of Engineering – Mataria (Architecture) ──
        MakePlan("BSc Architecture 2025",
            "Architectural Engineering", new[]
            {
                ("MATH101", 1, 1, true), ("PHYS101", 1, 1, true), ("ARCH201",1, 1, true), ("UNIV100", 1, 1, true), ("GEN101", 1, 1, true),
                ("MATH102", 1, 2, true), ("PHYS102", 1, 2, true), ("ARCH202",1, 2, true), ("GEN102",  1, 2, true), ("GEN150", 1, 2, true),
                ("ARCH301", 2, 1, true), ("CS101",   2, 1, false), ("GEN103", 2, 1, true),
                ("CE201",   2, 2, true), ("GEN202",  2, 2, false),
                ("ARCH401", 3, 1, true), ("CE301",   3, 1, true),
                ("CE302",   4, 1, false),
            });

        // ── Faculty of Engineering – Mataria (Mechanical) ──
        MakePlan("BSc Mechanical Engineering 2025",
            "Mechanical Engineering", new[]
            {
                ("MATH101", 1, 1, true), ("PHYS101",1, 1, true), ("CS101",  1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("MATH102", 1, 2, true), ("PHYS102",1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("MATH201", 2, 1, true), ("ME201",  2, 1, true), ("CE201",  2, 1, true), ("GEN103", 2, 1, true),
                ("ME202",   2, 2, true), ("MATH301",2, 2, true), ("GEN201", 2, 2, false),
                ("ME301",   3, 1, true), ("ME302",  3, 1, true), ("EE201",  3, 1, false), ("GEN202", 3, 1, false),
                ("CE301",   4, 1, false),
            });

        // ── Faculty of Engineering – Mataria (Electrical) ──
        MakePlan("BSc Electrical Engineering 2025",
            "Electrical Engineering", new[]
            {
                ("MATH101", 1, 1, true), ("PHYS101",1, 1, true), ("CS101",  1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("MATH102", 1, 2, true), ("PHYS102",1, 2, true), ("CS102",  1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("MATH201", 2, 1, true), ("EE201",  2, 1, true), ("GEN103", 2, 1, true),
                ("EE202",   2, 2, true), ("MATH301",2, 2, true), ("GEN201", 2, 2, false),
                ("EE301",   3, 1, true), ("EE302",  3, 1, true), ("ME201",  3, 1, false), ("GEN202", 3, 1, false),
                ("CS402",   4, 1, true),
            });

        // ── Faculty of Home Economics (Clinical Nutrition) ──
        MakePlan("BSc Clinical Nutrition 2025",
            "Clinical Nutrition", new[]
            {
                ("HE101",  1, 1, true), ("CHEM101",1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("HE102",  1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("HE201",  2, 1, true), ("HE202",  2, 1, true), ("GEN103", 2, 1, true),
                ("HE301",  2, 2, true), ("CHLD101",2, 2, false), ("GEN201", 2, 2, false),
                ("CHLD201",3, 1, false), ("GEN202", 3, 1, false),
                ("CS101",  4, 1, false),
            });

        // ── Faculty of Home Economics (Nutrition & Food Science) ──
        MakePlan("BSc Nutrition & Food Science 2025",
            "Nutrition & Food Science", new[]
            {
                ("HE101",  1, 1, true), ("CHEM101",1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("HE102",  1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("HE201",  2, 1, true), ("HE202",  2, 1, true), ("GEN103", 2, 1, true),
                ("HE301",  2, 2, true), ("GEN201", 2, 2, false),
                ("GEN202", 3, 1, false),
                ("CS101",  4, 1, false),
            });

        // ── Faculty of Home Economics (Textiles) ──
        MakePlan("BSc Textiles & Clothing 2025",
            "Textile & Clothing", new[]
            {
                ("TEX101", 1, 1, true), ("CHEM101",1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("TEX201", 1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("TEX301", 2, 1, true), ("HE202",  2, 1, false), ("GEN103", 2, 1, true),
                ("HE102",  2, 2, true), ("GEN201", 2, 2, false),
                ("GEN202", 3, 1, false),
                ("CS101",  4, 1, false),
            });

        // ── Faculty of Home Economics (Family & Childhood) ──
        MakePlan("BSc Family & Childhood Management 2025",
            "Family & Childhood Institution Management", new[]
            {
                ("CHLD101",1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("CHLD201",1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("HE202",  2, 1, false), ("GEN103", 2, 1, true),
                ("GEN201", 2, 2, false),
                ("GEN202", 3, 1, false),
                ("CS101",  4, 1, false),
            });

        // ── Faculty of Home Economics (General Stream) ──
        MakePlan("BSc General Stream 2025",
            "General Stream", new[]
            {
                ("HE101",  1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("HE102",  1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("CS101",  2, 1, false), ("GEN103", 2, 1, true),
                ("HE202",  2, 2, false), ("GEN201", 2, 2, false),
                ("GEN202", 3, 1, false),
            });

        await context.SaveChangesAsync();
        Console.WriteLine($"[Seed] AcademicPlans: {context.AcademicPlans.Local.Count} plans created.");
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
        // Year names are numeric-ish ("2023-2024") and read the same in both
        // cultures; semester names ("Fall" / "خريف", etc.) get the bilingual
        // JSON shape so the picker shows the operator's language.
        var fallName   = LocalizedJson.Of("خريف",  "Fall");
        var springName = LocalizedJson.Of("ربيع",  "Spring");
        var summerName = LocalizedJson.Of("صيف",   "Summer");

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
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = fallName,   Order = 1, StartDate = start, EndDate = start.AddMonths(4),  IsCurrent = current },
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = springName, Order = 2, StartDate = start.AddMonths(5), EndDate = start.AddMonths(9), IsCurrent = false },
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = summerName, Order = 3, StartDate = start.AddMonths(10),EndDate = end,                IsCurrent = false },
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
        // Bilingual JSON for every module display name. Rows that overlap a
        // permission manifest (academics, permissions, notifications, …)
        // also get refreshed at startup by PermissionManifestSynchronizer.
        var modules = new (string Key, string DisplayJson, string Icon, int Order)[]
        {
            ("dashboard",    LocalizedJson.Of("لوحة المعلومات",    "Dashboard"),            "LayoutDashboard", 0),
            ("users",        LocalizedJson.Of("إدارة المستخدمين",  "User Management"),      "Users",           1),
            ("structure",    LocalizedJson.Of("الهيكل الجامعي",     "University Structure"), "Building2",       2),
            ("programs",     LocalizedJson.Of("البرامج الأكاديمية", "Academic Programs"),    "BookOpen",        3),
            ("permissions",  LocalizedJson.Of("الصلاحيات والأدوار", "Permissions & Roles"),  "Shield",          4),
            ("sync",         LocalizedJson.Of("تكامل النظام",       "SIS Integration"),      "RefreshCw",       5),
            ("academics",    LocalizedJson.Of("الجدول الأكاديمي",   "Academic Timeline"),    "Calendar",        6),
            ("notifications",LocalizedJson.Of("الإشعرات",            "Notifications"),        "Bell",            7),
            ("system",       LocalizedJson.Of("النظام",             "System"),               "ShieldCheck",     9),
            ("courses",          LocalizedJson.Of("كتالوج المقررات",    "Course Catalog"),     "BookOpen",      8),
            ("course-offerings", LocalizedJson.Of("طرح المقررات",       "Course Offerings"),   "CalendarCheck", 10),
            ("schedule",         LocalizedJson.Of("الجدول الدراسي",     "Schedule"),           "Clock",         11),
        };
        var existing = await context.Modules.ToDictionaryAsync(m => m.ModuleKey);
        foreach (var (key, display, icon, order) in modules)
        {
            if (existing.TryGetValue(key, out var mod))
            {
                mod.DisplayName = display;
                mod.Icon = icon;
                mod.OrderNumber = order;
            }
            else
            {
                context.Modules.Add(new Module { Id = Guid.NewGuid(), ModuleKey = key, DisplayName = display, Icon = icon, OrderNumber = order });
            }
        }
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  5. AUTH RESOURCES
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedAuthResourcesAsync(CoreDbContext context)
    {
        var modules = await context.Modules.ToListAsync();
        var existing = await context.Resources.ToListAsync();

        void AddRes(string moduleKey, string key, string displayName, int order)
        {
            var modId = modules.First(m => m.ModuleKey == moduleKey).Id;
            var res = existing.FirstOrDefault(r => r.ModuleId == modId && r.Key == key);
            if (res != null)
            {
                res.DisplayName = displayName;
                res.OrderNumber = order;
            }
            else
            {
                var newRes = new Resource
                {
                    Id = Guid.NewGuid(),
                    ModuleId = modId,
                    Key = key,
                    DisplayName = displayName,
                    OrderNumber = order
                };
                context.Resources.Add(newRes);
                existing.Add(newRes);
            }
        }

        // One Resource per (module, key) — action verbs are per-row on
        // RolePermission.Action and StaffPermissionOverride.Action. Keys match
        // the canonical {module}.{resource}.{action} identity declared in
        // PermissionNames and the per-module manifests, so HasPermission
        // attribute lookups round-trip against these rows. DisplayName is
        // the bilingual JSON shape; PermissionManifestSynchronizer refreshes
        // the rows it owns on startup.
        AddRes("dashboard",    "dashboard",      LocalizedJson.Of("لوحة المعلومات",    "Dashboard"),         0);
        AddRes("users",        "users",          LocalizedJson.Of("المستخدمون",        "Users"),             0);
        AddRes("structure",    "structure",      LocalizedJson.Of("الهيكل",            "Structure"),         0);
        AddRes("programs",     "programs",       LocalizedJson.Of("البرامج",           "Programs"),          0);
        AddRes("permissions",  "permissions",    LocalizedJson.Of("الصلاحيات",         "Permissions"),       0);
        AddRes("permissions",  "roles",          LocalizedJson.Of("الأدوار",           "Roles"),             1);
        AddRes("sync",         "sync",           LocalizedJson.Of("مزامنة النظام",     "SIS Sync"),          0);
        AddRes("academics",    "academic-years", LocalizedJson.Of("الجدول الأكاديمي",  "Academic Timeline"), 0);
        AddRes("notifications","notifications",  LocalizedJson.Of("الإشعارات",         "Notifications"),     0);
        AddRes("system",       "audit-logs",     LocalizedJson.Of("سجل التدقيق",       "Audit Log"),         0);

        // Resources declared by manifest assemblies — seeded here so role
        // permission grants below can reference them. The PermissionManifestSynchronizer
        // reconciles metadata (DisplayName, Icon, OrderNumber) from manifests at startup.
        AddRes("courses",          "courses",         LocalizedJson.Of("كتالوج المقررات",  "Course Catalog"),     0);
        AddRes("courses",          "academic-plans",  LocalizedJson.Of("الخطط الدراسية",   "Academic Plans"),     1);
        AddRes("course-offerings", "course-offerings",LocalizedJson.Of("طرح المقررات",     "Course Offerings"),   0);
        AddRes("schedule",         "schedule-slots",  LocalizedJson.Of("مواعيد الجدول",    "Schedule Slots"),     0);

        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  6. ROLES
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedRolesAsync(CoreDbContext context)
    {
        // Role names render in role pickers + permission UIs; bilingual so
        // operator sees their language. Stored as {"ar":"…","en":"…"} JSON,
        // decoded in Get(s)RolesQueryHandler before reaching the client.
        var roles = new (string Json, bool System)[]
        {
            (LocalizedJson.Of("مسؤول النظام الأعلى", "Super Admin"),      true),
            (LocalizedJson.Of("مسؤول الكلية",         "Faculty Admin"),    false),
            (LocalizedJson.Of("رئيس القسم",           "Department Head"),  false),
            (LocalizedJson.Of("شؤون الطلاب",          "Registrar"),        false),
            (LocalizedJson.Of("المرشد الأكاديمي",     "Academic Advisor"), false),
            (LocalizedJson.Of("موظف",                  "Staff"),            false),
            (LocalizedJson.Of("مشاهد",                 "Viewer"),           false),
        };
        foreach (var (name, system) in roles)
            context.Roles.Add(new Role { Id = Guid.NewGuid(), Name = name, IsSystemRole = system });
        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  7. ROLE PERMISSIONS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedRolePermissionsAsync(CoreDbContext context, ManifestActionExpander actionExpander)
    {
        var roles = await context.Roles.ToListAsync();
        var resources = await context.Resources.Include(r => r.Module).ToListAsync();

        // Roles + resources are stored bilingually ({"ar":"…","en":"…"}); the
        // seeder's grant statements key by their English label, so index by
        // the "en" subkey (falls back to the literal for plain-text rows).
        var roleMap = roles.ToDictionary(r => LocalizedJson.Extract(r.Name, "en"), r => r.Id);
        var resourceMap = resources.GroupBy(r => (r.Module.ModuleKey, r.Key)).ToDictionary(g => g.Key, g => g.First());

        // Per-action rows. Existing rows are keyed by (RoleId, ResourceId, Action)
        // to respect the IX_RolePermissions_RoleId_ResourceId_Action unique index.
        var existing = await context.RolePermissions
            .ToDictionaryAsync(rp => (rp.RoleId, rp.ResourceId, rp.Action));

        void Grant(string roleName, string moduleKey, string resourceKey, string topAction)
        {
            if (!resourceMap.TryGetValue((moduleKey, resourceKey), out var res)) return;
            if (!roleMap.TryGetValue(roleName, out var roleId)) return;

            // Expand the granted verb through the resource manifest's forward implies
            // graph (a role grant is an allow). Every granted resource is manifest-
            // declared, so this is the single source of truth — no CRUD-ladder fallback.
            foreach (var action in actionExpander.ExpandActionNames(moduleKey, resourceKey, topAction))
            {
                if (existing.ContainsKey((roleId, res.Id, action))) continue;

                var newRow = new RolePermission(roleId, res.Id, action)
                {
                    Id = Guid.NewGuid(),
                };
                context.RolePermissions.Add(newRow);
                existing[(roleId, res.Id, action)] = newRow;
            }
        }

        // Super Admin — full access to every seeded resource. Resources declare
        // different verb sets (e.g. notifications only has View/Insert) and
        // ExpandActionNames returns an empty set for undeclared verbs, so walk
        // the ladder down until the manifest yields a non-empty expansion.
        var superAdminVerbLadder = new[] { "Delete", "Open", "EditClose", "Insert", "View" };
        foreach (var res in resources)
        {
            foreach (var verb in superAdminVerbLadder)
            {
                if (actionExpander.ExpandActionNames(res.Module.ModuleKey, res.Key, verb).Count == 0) continue;
                Grant("Super Admin", res.Module.ModuleKey, res.Key, verb);
                break;
            }
        }

        // Faculty Admin
        Grant("Faculty Admin", "dashboard",     "dashboard",      "View");
        Grant("Faculty Admin", "users",         "users",          "EditClose");
        Grant("Faculty Admin", "structure",     "structure",      "View");
        Grant("Faculty Admin", "programs",      "programs",       "Insert");
        Grant("Faculty Admin", "permissions",   "permissions",    "View");
        Grant("Faculty Admin", "permissions",   "roles",          "View");
        Grant("Faculty Admin", "academics",     "academic-years", "View");
        Grant("Faculty Admin", "notifications", "notifications",  "Insert");
        Grant("Faculty Admin", "courses",          "courses",         "EditClose");
        Grant("Faculty Admin", "courses",          "academic-plans",  "EditClose");
        Grant("Faculty Admin", "course-offerings", "course-offerings","EditClose");
        Grant("Faculty Admin", "schedule",         "schedule-slots",  "EditClose");

        // Department Head
        Grant("Department Head", "dashboard",     "dashboard",      "View");
        Grant("Department Head", "users",         "users",          "EditClose");
        Grant("Department Head", "structure",     "structure",      "View");
        Grant("Department Head", "programs",      "programs",       "View");
        Grant("Department Head", "academics",     "academic-years", "View");
        Grant("Department Head", "notifications", "notifications",  "View");
        Grant("Department Head", "courses",          "courses",         "EditClose");
        Grant("Department Head", "courses",          "academic-plans",  "EditClose");
        Grant("Department Head", "course-offerings", "course-offerings","EditClose");
        Grant("Department Head", "schedule",         "schedule-slots",  "EditClose");

        // Registrar
        Grant("Registrar", "dashboard", "dashboard",      "View");
        Grant("Registrar", "users",     "users",          "EditClose");
        Grant("Registrar", "structure", "structure",      "View");
        Grant("Registrar", "programs",  "programs",       "Insert");
        Grant("Registrar", "academics", "academic-years", "View");
        Grant("Registrar", "courses",       "courses",         "Insert");
        Grant("Registrar", "courses",       "academic-plans",  "View");
        Grant("Registrar", "course-offerings", "course-offerings", "Insert");

        // Academic Advisor
        Grant("Academic Advisor", "dashboard",     "dashboard",     "View");
        Grant("Academic Advisor", "users",         "users",         "View");
        Grant("Academic Advisor", "structure",     "structure",     "View");
        Grant("Academic Advisor", "programs",      "programs",      "View");
        Grant("Academic Advisor", "notifications", "notifications", "View");
        Grant("Academic Advisor", "courses",          "courses",         "View");
        Grant("Academic Advisor", "courses",          "academic-plans",  "View");
        Grant("Academic Advisor", "course-offerings", "course-offerings","View");
        Grant("Academic Advisor", "schedule",         "schedule-slots",  "View");

        // Staff (basic)
        Grant("Staff", "dashboard",     "dashboard",     "View");
        Grant("Staff", "users",         "users",         "View");
        Grant("Staff", "notifications", "notifications", "View");
        Grant("Staff", "course-offerings", "course-offerings", "View");
        Grant("Staff", "schedule",         "schedule-slots",  "View");

        // Viewer
        Grant("Viewer", "dashboard", "dashboard", "View");
        Grant("Viewer", "users",     "users",     "View");

        await context.SaveChangesAsync();
    }

    // ════════════════════════════════════════════════════════════════
    //  8. STAFF  (Super admin credentials: nationalId=29801011234567, password=admin123)
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStaffAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        var nodes = await context.StructureNodes.ToListAsync();
        StructureNode? FindNode(string name) =>
            nodes.FirstOrDefault(n => n.Name == name)
            ?? nodes.FirstOrDefault(n => n.Name.Contains(name));

        var adminPwd = passwordHasher.HashPassword("admin123");
        // JobTitle values are bilingual JSON; the Name and Role columns stay
        // literal because they're personal names / unique role identifiers.
        var defs = new (string Emp, string Name, string NID, DateTime DOB, string Phone, string Email, string Role, string Job, string NodeName)[]
        {
            ("ADMIN-001", "Super Admin User",      "29801011234567", new(1985, 6, 15),  "01000000000", "superadmin@capital.edu.eg",        "Super Admin",      LocalizedJson.Of("مسؤول النظام",            "System Administrator"),           "Capital University"),
            ("EMP001",    "Dr. Ibrahim Abdullah",  "27801011234567", new(1978, 1, 1),   "01111111111", "ibrahim@capital.edu.eg",           "Super Admin",      LocalizedJson.Of("مدير النظام",              "System Administrator"),           "Capital University"),
            ("FAC-001",   "Dr. Fatima Hassan",     "28501011234567", new(1978, 3, 20),  "01111111111", "fatima.hassan@capital.edu.eg",     "Faculty Admin",    LocalizedJson.Of("عميد الكلية",              "Faculty Dean"),                   "Home Economics"),
            ("HOD-001",   "Dr. Khaled Ibrahim",    "28102021234567", new(1975, 11, 5),  "01111111112", "khaled.ibrahim@capital.edu.eg",    "Department Head",  LocalizedJson.Of("رئيس قسم التغذية الإكلينيكية","Head of Clinical Nutrition"),     "Clinical Nutrition"),
            ("REG-001",   "Ms. Aisha Mahmoud",     "29003031234567", new(1985, 7, 12),  "01111111113", "aisha.mahmoud@capital.edu.eg",     "Registrar",        LocalizedJson.Of("مسؤول شؤون الطلاب الأقدم", "Senior Registrar Officer"),       "Capital University"),
            ("ADV-001",   "Dr. Omar El-Sayed",     "27504041234567", new(1982, 9, 28),  "01111111114", "omar.elsayed@capital.edu.eg",      "Academic Advisor", LocalizedJson.Of("مرشد أكاديمي للطلاب",      "Student Academic Advisor"),       "Nutrition & Food Science"),
            ("STF-001",   "Mr. Tamer Said",        "29205051234567", new(1990, 1, 15),  "01111111115", "tamer.said@capital.edu.eg",        "Staff",            LocalizedJson.Of("أخصائي دعم تقني",          "IT Support Specialist"),          "Capital University"),
            ("VWR-001",   "Ms. Nadia Youssef",     "29306061234567", new(1992, 4, 8),   "01111111116", "nadia.youssef@capital.edu.eg",     "Viewer",           LocalizedJson.Of("مراجع خارجي",              "External Auditor"),               "Capital University"),
            ("FAC-002",   "Dr. Ahmed Abdel-Rahman","27807071234567", new(1970, 8, 18),  "01111111117", "ahmed.abdelrahman@capital.edu.eg", "Faculty Admin",    LocalizedJson.Of("وكيل الكلية للشؤون الأكاديمية","Vice Dean for Academic Affairs"), "Mataria"),
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
        StructureNode? FindProgram(string name) =>
            nodes.FirstOrDefault(n => n.Name == name && n.Type == StructureNodeType.Program)
            ?? nodes.FirstOrDefault(n => n.Name.Contains(name) && n.Type == StructureNodeType.Program);

        var pwd = passwordHasher.HashPassword("123456");
        var defs = new (string Code, string Name, string NID, DateTime DOB, string Phone, string Email, string NodeName)[]
        {
            ("20250001", "Ahmed Mohamed Ali",        "30201011234567", new(2002, 1, 1),  "01000000001", "ahmed.mohamed@capital.edu.eg",   "Nutrition & Food Science"),
            ("20250002", "Sara Mahmoud Hassan",      "30202021234567", new(2002, 2, 2),  "01000000002", "sara.hassan@capital.edu.eg",      "Nutrition & Food Science"),
            ("20250003", "Mohamed Khaled Ibrahim",   "30203031234567", new(2003, 3, 3),  "01000000003", "mohamed.ibrahim@capital.edu.eg",  "Nutrition & Food Science"),
            ("20250004", "Nourhan Atef El-Sayed",    "30204041234567", new(2001, 5, 15), "01000000004", "nourhan.atef@capital.edu.eg",     "Nutrition & Food Science"),
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

        // Group students by program so we can distribute them across actual Level nodes
        foreach (var group in defs.GroupBy(d => d.NodeName))
        {
            var programNode = FindProgram(group.Key);
            if (programNode == null)
            {
                Console.WriteLine($"[Seed] Students: skipping program '{group.Key}' — node not found.");
                continue;
            }

            var levelNodes = nodes
                .Where(n => n.ParentId == programNode.Id && n.Type == StructureNodeType.Level)
                .OrderBy(n => n.Order)
                .ToArray();

            if (levelNodes.Length == 0)
            {
                Console.WriteLine($"[Seed] Students: skipping program '{group.Key}' — no Level children found.");
                continue;
            }

            var levelIdx = 0;
            foreach (var d in group)
            {
                var node = levelNodes[levelIdx % levelNodes.Length];
                levelIdx++;

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
        var roles = (await context.Roles.ToListAsync())
            .ToDictionary(r => LocalizedJson.Extract(r.Name, "en"));
        var nodes = await context.StructureNodes.ToListAsync();

        StructureNode? FindNode(string name) =>
            nodes.FirstOrDefault(n => n.Name == name)
            ?? nodes.FirstOrDefault(n => n.Name.Contains(name));

        // NodeName=null means "global structural" — the row has no structural restriction
        // and the runtime PermissionService filter accepts it regardless of header.
        // Reserved for Super Admin; every other role is tenanted to a real node.
        var defs = new (string EmpCode, string RoleName, string Year, string Semester, string? NodeName)[]
        {
            ("ADMIN-001", "Super Admin",      ScopeKeys.Global, ScopeKeys.Global, null),
            ("EMP001",    "Super Admin",      ScopeKeys.Global, ScopeKeys.Global, null),
            ("FAC-001",   "Faculty Admin",    ScopeKeys.Global, ScopeKeys.Global, "Home Economics"),
            ("HOD-001",   "Department Head",  ScopeKeys.Global, ScopeKeys.Global, "Clinical Nutrition"),
            ("REG-001",   "Registrar",        ScopeKeys.Global, ScopeKeys.Global, "Capital University"),
            ("ADV-001",   "Academic Advisor", ScopeKeys.Global, ScopeKeys.Global, "Nutrition & Food Science"),
            ("STF-001",   "Staff",            ScopeKeys.Global, ScopeKeys.Global, "Capital University"),
            ("VWR-001",   "Viewer",           ScopeKeys.Global, ScopeKeys.Global, "Capital University"),
            ("FAC-002",   "Faculty Admin",    ScopeKeys.Global, ScopeKeys.Global, "Mataria"),
        };

        var existing = await context.StaffRoles.ToListAsync();

        // Cleanup rows that were stored at a specific year/semester scope —
        // all seeded role assignments should use Global scope.
        var stale = existing.Where(r => r.Year != ScopeKeys.Global || r.Semester != ScopeKeys.Global).ToList();
        if (stale.Count > 0)
        {
            context.StaffRoles.RemoveRange(stale);
            await context.SaveChangesAsync();
            Console.WriteLine($"[Seed] StaffRoles: cleaned {stale.Count} stale scoped assignment(s).");
            existing = await context.StaffRoles.ToListAsync();
        }

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
    // 11. STAFF PERMISSION OVERRIDES
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStaffPermissionOverridesAsync(CoreDbContext context)
    {
        var staff = await context.Staffs.ToDictionaryAsync(s => s.EmployeeCode);
        var resources = await context.Resources.Include(r => r.Module).ToListAsync();

        var resourceMap = resources.ToDictionary(r => (r.Module.ModuleKey, r.Key));

        var existing = await context.StaffPermissions
            .ToDictionaryAsync(sp => (sp.StaffId, sp.ResourceId, sp.Action));

        var added = 0;

        void AddOverride(string empCode, string moduleKey, string resourceKey, string action, OverrideType type, string? nodeName = null)
        {
            if (!staff.TryGetValue(empCode, out var s)) return;
            if (!resourceMap.TryGetValue((moduleKey, resourceKey), out var res)) return;

            if (existing.ContainsKey((s.Id, res.Id, action))) return;

            var nodes = context.StructureNodes.IgnoreQueryFilters().ToList();
            Guid? nodeId = null;
            string? nodePath = null;
            if (nodeName != null)
            {
                var node = nodes.FirstOrDefault(n => n.Name == nodeName)
                    ?? nodes.FirstOrDefault(n => n.Name.Contains(nodeName));
                if (node != null)
                {
                    nodeId = node.Id;
                    nodePath = node.Path;
                }
            }

            context.StaffPermissions.Add(new StaffPermissionOverride(s.Id, res.Id, action, type, ScopeKeys.Global, ScopeKeys.Global)
            {
                StructureNodeId = nodeId,
                StructureNodePath = nodePath,
            });
            existing[(s.Id, res.Id, action)] = null!;
            added++;
        }

        // STF-001 (Tamer Said – IT Support, role "Staff"): temporarily denied
        // from editing users, but allowed to view notifications.
        AddOverride("STF-001", "users",         "users",          "Edit",    OverrideType.Deny);
        AddOverride("STF-001", "notifications", "notifications",  "View",    OverrideType.Allow);

        // VWR-001 (Nadia Youssef – External Auditor, role "Viewer"): denied
        // access to structure data, but allowed to view programs.
        AddOverride("VWR-001", "structure", "structure", "View", OverrideType.Deny);

        // FAC-002 (Ahmed Abdel-Rahman – Vice Dean, Mataria): explicitly denied
        // from managing Home Economics resources (scoped to that faculty).
        AddOverride("FAC-002", "programs", "programs", "Insert", OverrideType.Deny, "Mataria");

        await context.SaveChangesAsync();
        Console.WriteLine($"[Seed] StaffPermissions: +{added} overrides created.");
    }

    // ════════════════════════════════════════════════════════════════
    // 12. NOTIFICATIONS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedNotificationsAsync(CoreDbContext context)
    {
        var staff = await context.Staffs.ToListAsync();
        var adminId = staff.FirstOrDefault(s => s.EmployeeCode == "ADMIN-001")?.Id;
        var regId = staff.FirstOrDefault(s => s.EmployeeCode == "REG-001")?.Id;
        var facId = staff.FirstOrDefault(s => s.EmployeeCode == "FAC-001")?.Id;

        if (adminId == null || regId == null || facId == null)
        {
            Console.WriteLine("[Seed] Notifications: skipping — required staff not found.");
            return;
        }

        // Title and Message are bilingual JSON. NotificationService decodes
        // both via ILocalizationService.Get<string>(json) on every read.
        context.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), RecipientUserId = adminId.Value,
                Title   = LocalizedJson.Of("مرحبًا بك في بوابة جامعة العاصمة", "Welcome to Capital University Portal"),
                Message = LocalizedJson.Of("تم إنشاء حسابك بصلاحيات مسؤول النظام الأعلى.", "Your account has been created with Super Administrator privileges."),
                Type = NotificationType.Info, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = adminId.Value,
                Title   = LocalizedJson.Of("تحديث العام الأكاديمي", "Academic Year Update"),
                Message = LocalizedJson.Of("تم تفعيل العام الأكاديمي 2025-2026. يُرجى مراجعة جدول الفصول.", "The 2025-2026 academic year has been activated. Please review the semester schedule."),
                Type = NotificationType.Warning, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = adminId.Value,
                Title   = LocalizedJson.Of("موافقات معلقة", "Pending Approvals"),
                Message = LocalizedJson.Of("هناك ٣ طلبات تسجيل معلقة تنتظر اعتمادك.", "There are 3 pending registration approvals requiring your attention."),
                Type = NotificationType.Info, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = regId.Value,
                Title   = LocalizedJson.Of("اكتمل الاستيراد المجمّع", "Bulk Import Complete"),
                Message = LocalizedJson.Of("اكتمل استيراد بيانات الطلاب. تم إنشاء ٤٧ سجلًا جديدًا وتجاوز سجلين مكررين.", "Student data import completed. 47 new records created, 2 duplicates skipped."),
                Type = NotificationType.Info, IsRead = false },
            new Notification { Id = Guid.NewGuid(), RecipientUserId = facId.Value,
                Title   = LocalizedJson.Of("استحقاق تقرير القسم", "Department Report Due"),
                Message = LocalizedJson.Of("يستحق تقرير نهاية الفصل الخاص بالقسم في 15 ديسمبر.", "The semester-end departmental report is due by December 15."),
                Type = NotificationType.Warning, IsRead = false }
        );

        await context.SaveChangesAsync();
    }
}
