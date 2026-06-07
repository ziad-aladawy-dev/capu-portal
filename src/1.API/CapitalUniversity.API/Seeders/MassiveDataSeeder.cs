using CapitalUniversity.Core.Domain.Authorization;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Courses;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Domain.Semsters;
using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Modules.CourseOffering.Domain;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.Schedule.Domain;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Student.Domain;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Payments.Domain;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapitalUniversity.API.Seeders;

public static class MassiveDataSeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        await ExpandCoursesAsync(context);
        await SeedAcademicPlansAsync(context);
        await ExpandStudentsAsync(context, passwordHasher);
        await SeedCourseOfferingsAsync(context);
        await SeedScheduleSlotsAsync(context);
        //await SeedWorkflowsAsync(context);
        //await SeedStudentServicesAsync(context);
        //await SeedStudentServiceRequestsAsync(context);
        await ExpandPaymentsAsync(context);
        await SeedStudentProfileRecordsAsync(context);
    }

    private static async Task ExpandCoursesAsync(CoreDbContext context)
    {
        if (await context.Courses.CountAsync() > 10) return;

        var seed = new (string Code, string TitleAr, string TitleEn, int CreditHours, CourseCategory Category)[]
        {
            ("CS101",   "مقدمة في علوم الحاسب",          "Introduction to Computer Science", 3, CourseCategory.ProgramRequirement),
            ("CS201",   "هياكل البيانات والخوارزميات",   "Data Structures and Algorithms",   3, CourseCategory.ProgramRequirement),
            ("MATH101", "تفاضل وتكامل (1)",              "Calculus I",                       4, CourseCategory.FacultyRequirement),
            ("UNIV100", "التفكير الناقد",                 "Critical Thinking",                2, CourseCategory.UniversityRequirement),
            ("GEN150",  "مهارات الإلقاء",                 "Public Speaking",                  2, CourseCategory.GeneralEducation),
            ("CS-ELC1", "موضوعات في الذكاء الاصطناعي",   "Topics in AI",                     3, CourseCategory.Elective),

            ("CS102",   "برمجة حاسبات (2)",               "Computer Programming II",          3, CourseCategory.ProgramRequirement),
            ("CS202",   "تصميم قواعد البيانات",           "Database Design",                  3, CourseCategory.ProgramRequirement),
            ("CS301",   "هندسة البرمجيات",                "Software Engineering",             3, CourseCategory.ProgramRequirement),
            ("CS401",   "التعلم الآلي",                   "Machine Learning",                 3, CourseCategory.ProgramRequirement),
            ("CS402",   "تأمين المعلومات",                "Information Security",             3, CourseCategory.ProgramRequirement),

            ("MATH102", "تفاضل وتكامل (2)",              "Calculus II",                      4, CourseCategory.FacultyRequirement),
            ("MATH201", "المعادلات التفاضلية",           "Differential Equations",           3, CourseCategory.FacultyRequirement),
            ("MATH301", "الاحتمالات والإحصاء",           "Probability & Statistics",         3, CourseCategory.FacultyRequirement),
            ("PHYS101", "الفيزياء (1)",                  "Physics I",                        4, CourseCategory.FacultyRequirement),
            ("PHYS102", "الفيزياء (2)",                  "Physics II",                       4, CourseCategory.FacultyRequirement),
            ("CHEM101", "الكيمياء العامة",               "General Chemistry",                3, CourseCategory.FacultyRequirement),

            ("CE201",   "ميكانيكا الهندسة",              "Engineering Mechanics",            3, CourseCategory.ProgramRequirement),
            ("CE202",   "مقاومة المواد",                 "Strength of Materials",            3, CourseCategory.ProgramRequirement),
            ("CE301",   "تحليل الإنشاءات",               "Structural Analysis",              3, CourseCategory.ProgramRequirement),
            ("CE302",   "هندسة الأساسات",                "Foundation Engineering",           3, CourseCategory.ProgramRequirement),
            ("CE401",   "هندسة النقل",                   "Transportation Engineering",       3, CourseCategory.ProgramRequirement),

            ("ARCH201", "تاريخ العمارة",                 "History of Architecture",          2, CourseCategory.ProgramRequirement),
            ("ARCH202", "الرسم المعماري",                "Architectural Drawing",            3, CourseCategory.ProgramRequirement),
            ("ARCH301", "التصميم المعماري (1)",          "Architectural Design I",           4, CourseCategory.ProgramRequirement),
            ("ARCH401", "التخطيط العمراني",              "Urban Planning",                   3, CourseCategory.ProgramRequirement),

            ("ME201",   "الديناميكا الحرارية",           "Thermodynamics",                   3, CourseCategory.ProgramRequirement),
            ("ME202",   "ميكانيكا الموائع",              "Fluid Mechanics",                  3, CourseCategory.ProgramRequirement),
            ("ME301",   "انتقال الحرارة",                "Heat Transfer",                    3, CourseCategory.ProgramRequirement),
            ("ME302",   "هندسة التصنيع",                 "Manufacturing Engineering",        3, CourseCategory.ProgramRequirement),

            ("EE201",   "الدوائر الكهربائية",            "Electric Circuits",                3, CourseCategory.ProgramRequirement),
            ("EE202",   "الإلكترونيات",                  "Electronics",                      3, CourseCategory.ProgramRequirement),
            ("EE301",   "أنظمة التحكم",                  "Control Systems",                  3, CourseCategory.ProgramRequirement),
            ("EE302",   "الاتصالات التناظرية",           "Analog Communications",            3, CourseCategory.ProgramRequirement),

            ("HE101",   "التغذية الأساسية",              "Basic Nutrition",                  3, CourseCategory.ProgramRequirement),
            ("HE102",   "علوم الأطعمة",                  "Food Science",                     3, CourseCategory.ProgramRequirement),
            ("HE201",   "التغذية العلاجية",              "Clinical Nutrition",               3, CourseCategory.ProgramRequirement),
            ("HE202",   "إدارة المؤسسات الغذائية",       "Food Service Management",          3, CourseCategory.ProgramRequirement),
            ("HE301",   "التغذية المجتمعية",             "Community Nutrition",              3, CourseCategory.ProgramRequirement),
            ("TEX101",  "المنسوجات والملابس",            "Textiles & Clothing",              3, CourseCategory.ProgramRequirement),
            ("TEX201",  "تصميم الأزياء",                 "Fashion Design",                   3, CourseCategory.ProgramRequirement),
            ("TEX301",  "تكنولوجيا الملابس",             "Apparel Technology",               3, CourseCategory.ProgramRequirement),
            ("CHLD101", "تنمية الطفل",                   "Child Development",                3, CourseCategory.ProgramRequirement),
            ("CHLD201", "إدارة رياض الأطفال",            "Kindergarten Management",          3, CourseCategory.ProgramRequirement),

            ("BME201",  "الموائع الحيوية",               "Biofluid Mechanics",               3, CourseCategory.ProgramRequirement),
            ("BME202",  "الإشارات الحيوية",              "Biosignals",                       3, CourseCategory.ProgramRequirement),
            ("BME301",  "الأجهزة الطبية",                "Medical Devices",                  3, CourseCategory.ProgramRequirement),

            ("GEN101",  "مهارات الحاسب",                 "Computer Skills",                  2, CourseCategory.GeneralEducation),
            ("GEN102",  "اللغة العربية",                 "Arabic Language",                  2, CourseCategory.UniversityRequirement),
            ("GEN103",  "اللغة الإنجليزية",              "English Language",                 2, CourseCategory.UniversityRequirement),
            ("GEN201",  "تاريخ مصر الحديث",              "Modern Egyptian History",          2, CourseCategory.GeneralEducation),
            ("GEN202",  "التربية البيئية",               "Environmental Education",          2, CourseCategory.GeneralEducation),
        };

        var existing = await context.Courses.Select(c => c.Code).ToHashSetAsync();
        foreach (var (code, ar, en, hours, category) in seed)
        {
            if (existing.Contains(code)) continue;
            context.Courses.Add(new Course
            {
                Id = Guid.NewGuid(),
                Code = code,
                Title = LocalizedJson.Of(ar, en),
                CreditHours = hours,
                Category = category,
                IsActive = true,
            });
        }
        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] Courses: expanded.");
    }

    private static async Task SeedAcademicPlansAsync(CoreDbContext context)
    {
        if (await context.AcademicPlans.AnyAsync()) return;

        var nodes = await context.StructureNodes.ToListAsync();
        var courses = await context.Courses.ToDictionaryAsync(c => c.Code);

        var programs = nodes.Where(n => n.Type == StructureNodeType.Program).ToList();
        var years = await context.AcademicYears.OrderBy(y => y.Name).ToListAsync();
        var effectiveFrom = years.FirstOrDefault()?.StartDate ?? new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        StructureNode? GetProgram(string name)
        {
            var found = programs.FirstOrDefault(n =>
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(n.Name);
                    return dict != null && dict.TryGetValue("en", out var en) && en.Contains(name, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return n.Name.Contains(name, StringComparison.OrdinalIgnoreCase);
                }
            });
            if (found == null)
                Console.WriteLine($"[MassSeed] Plan: program '{name}' not found in structure — skipping.");
            return found;
        }

        int GetLevelCount(Guid programId)
        {
            var childLevels = nodes.Where(n => n.ParentId == programId && n.Type == StructureNodeType.Level).ToList();
            return Math.Max(childLevels.Count, 4);
        }

        Course C(string code) => courses.GetValueOrDefault(code) ?? throw new InvalidOperationException($"Course '{code}' not found.");

        Guid? MakePlan(string name, string programName, (string Code, int Level, int Semester, bool Mandatory)[] planCourses)
        {
            var prog = GetProgram(programName);
            if (prog == null) return null;
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
                if (!courses.ContainsKey(code))
                {
                    Console.WriteLine($"[MassSeed] Plan '{name}': skipping missing course '{code}'.");
                    continue;
                }
                plan.PlanCourses.Add(new AcademicPlanCourse
                {
                    AcademicPlanId = plan.Id,
                    CourseId = C(code).Id,
                    Level = level,
                    Semester = sem,
                    IsMandatory = mandatory,
                });
            }

            context.AcademicPlans.Add(plan);
            return plan.Id;
        }

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

        MakePlan("BSc General Stream 2025",
            "General Stream", new[]
            {
                ("HE101",  1, 1, true), ("UNIV100",1, 1, true), ("GEN101", 1, 1, true),
                ("HE102",  1, 2, true), ("GEN102", 1, 2, true), ("GEN150", 1, 2, true),
                ("CS101",  2, 1, false), ("GEN103", 2, 1, true),
                ("HE202",  2, 2, false), ("GEN201", 2, 2, false),
                ("GEN202", 3, 1, false),
            });

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

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] AcademicPlans: {context.AcademicPlans.Local.Count} plans created.");
    }

    private static async Task ExpandStudentsAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        if (await context.Students.CountAsync() > 30) return;

        var nodes = await context.StructureNodes.ToListAsync();
        var pwd = passwordHasher.HashPassword("123456");

        var levels = nodes.Where(n => n.Type == StructureNodeType.Level).ToList();
        var programs = nodes.Where(n => n.Type == StructureNodeType.Program).ToDictionary(n => n.Id);

        var programLevels = new Dictionary<Guid, List<StructureNode>>();
        foreach (var lvl in levels)
        {
            if (lvl.ParentId == null) continue;
            if (!programs.ContainsKey(lvl.ParentId.Value)) continue;
            if (!programLevels.ContainsKey(lvl.ParentId.Value))
                programLevels[lvl.ParentId.Value] = new List<StructureNode>();
            programLevels[lvl.ParentId.Value].Add(lvl);
        }

        var existingCodes = await context.Students.Select(s => s.StudentCode).ToHashSetAsync();
        var existingNids = await context.Students.Select(s => s.NationalId).ToHashSetAsync();
        var nextCode = 20250018;
        var added = 0;

        void AddStudentsToProgram(string programName, int countPerLevel, Func<int, string, string> nameGen, Func<int, string> nidGen)
        {
            var prog = programs.Values.FirstOrDefault(p =>
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(p.Name);
                    return dict != null && dict.TryGetValue("en", out var en) && en.Contains(programName, StringComparison.OrdinalIgnoreCase);
                }
                catch
                {
                    return p.Name.Contains(programName, StringComparison.OrdinalIgnoreCase);
                }
            });
            if (prog == null)
            {
                Console.WriteLine($"[MassSeed] Students: program '{programName}' not found.");
                return;
            }

            if (!programLevels.TryGetValue(prog.Id, out var progLevels)) return;

            foreach (var level in progLevels)
            {
                for (var i = 1; i <= countPerLevel; i++)
                {
                    var code = nextCode++.ToString();
                    if (existingCodes.Contains(code)) continue;
                    var nid = nidGen(i);
                    if (existingNids.Contains(nid)) continue;
                    var suffix = $"{progLevels.IndexOf(level) + 1}-{i}";
                    context.Students.Add(new Student
                    {
                        Id = Guid.NewGuid(),
                        StudentCode = code,
                        Name = nameGen(i, suffix),
                        NationalId = nid,
                        BirthDate = new DateTime(2002 + (i % 3), 1 + (i % 11), 1 + (i % 27), 0, 0, 0, DateTimeKind.Utc),
                        PhoneNumber = $"0100000{100 + added + i:D4}",
                        Email = $"student{code}@capital.edu.eg",
                        StructureNodeId = level.Id,
                        PasswordHash = pwd,
                        PasswordExpiry = DateTime.UtcNow.AddYears(5),
                        IsActive = true,
                    });
                    existingNids.Add(nid);
                    added++;
                }
            }
        }

        AddStudentsToProgram("Computer & Systems Engineering", 3,
            (i, s) => s switch
            {
                "1-1" => "Ahmed Yasser Ali", "1-2" => "Mariam Tamer Hany", "1-3" => "Khaled Waleed Samir",
                "2-1" => "Nour Mohamed Essam", "2-2" => "Dina Ashraf Hassan", "2-3" => "Youssef Hani Khaled",
                "3-1" => "Sama Mostafa Adel", "3-2" => "Omar Sherif Galal", "3-3" => "Laila Mahmoud Nabil",
                _ => $"Student CSE {s}",
            },
            i => $"3021818{100 + i:D4}67");

        AddStudentsToProgram("Civil Engineering", 2,
            (i, s) => s switch
            {
                "1-1" => "Hassan Mohamed Ramadan", "1-2" => "Fatma Alaa El-Din",
                "2-1" => "Kareem Hassan Youssef", "2-2" => "Mona Gamal Abdou",
                "3-1" => "Mostafa Ahmed Shawky", "3-2" => "Hagar Adel Mahmoud",
                "4-1" => "Islam Tamer Mohamed", "4-2" => "Nourhan Said Hassan",
                _ => $"Student CE {s}",
            },
            i => $"3021919{100 + i:D4}67");

        AddStudentsToProgram("Architectural Engineering", 2,
            (i, s) => s switch
            {
                "1-1" => "Nada Khaled Omar", "1-2" => "Mohamed Ashraf Amin",
                "2-1" => "Sara Gamal El-Deen", "2-2" => "Ahmed Mahmoud Fathy",
                "3-1" => "Mariam Hany Sobhy", "3-2" => "Yassin Tarek Mohamed",
                "4-1" => "Noorhan Mostafa Kamel", "4-2" => "Ali Hassan Abdelaziz",
                _ => $"Student ARCH {s}",
            },
            i => $"3022020{100 + i:D4}67");

        AddStudentsToProgram("Mechanical Engineering", 2,
            (i, s) => s switch
            {
                "1-1" => "Mahmoud Ahmed Ibrahim", "1-2" => "Aya Elsayed Hassan",
                "2-1" => "Ibrahim Mostafa Gomaa", "2-2" => "Shaimaa Reda Ali",
                "3-1" => "Tamer Samir Waheed", "3-2" => "Heba Allah Mahmoud",
                "4-1" => "Mohamed Gamal Abdel-Fattah", "4-2" => "Amira Said Youssef",
                _ => $"Student ME {s}",
            },
            i => $"3022121{100 + i:D4}67");

        AddStudentsToProgram("Electrical Engineering", 2,
            (i, s) => s switch
            {
                "1-1" => "Ziad Hossam El-Din", "1-2" => "Reem Atef Mostafa",
                "2-1" => "Murad Walid Nabil", "2-2" => "Nada Ibrahim Abdel-Fattah",
                "3-1" => "Hussein Mohamed Nagy", "3-2" => "Salma Khaled Emam",
                "4-1" => "Yara Ahmed Mahmoud", "4-2" => "Mostafa Hany Shokry",
                _ => $"Student EE {s}",
            },
            i => $"3022222{100 + i:D4}67");

        AddStudentsToProgram("Clinical Nutrition", 2,
            (i, s) => s switch
            {
                "1-1" => "Asmaa Waleed Tawfik", "1-2" => "Mohamed Samy Abdelaziz",
                "2-1" => "Dalia Ashraf El-Sayed", "2-2" => "Hazem Mohamed Mohsen",
                "3-1" => "Eman Nabil Fouad", "3-2" => "Khaled Ahmed Abdel-Rahman",
                "4-1" => "Shorouk Mohamed Magdy", "4-2" => "Ahmed Hassan El-Gammal",
                _ => $"Student CN {s}",
            },
            i => $"3022323{100 + i:D4}67");

        AddStudentsToProgram("Nutrition & Food Science", 2,
            (i, s) => s switch
            {
                "1-1" => "Mai Tamer Hassan", "1-2" => "Abdel-Rahman Adel Nour",
                "2-1" => "Noha Khaled Ibrahim", "2-2" => "Tarek Yehia Mahmoud",
                "3-1" => "Rania Mostafa Kamal", "3-2" => "Hamza Ali Youssef",
                "4-1" => "Sohila Ahmed Mohamed", "4-2" => "Islam Said Khalil",
                _ => $"Student NFS {s}",
            },
            i => $"3022424{100 + i:D4}67");

        AddStudentsToProgram("Textile & Clothing", 2,
            (i, s) => s switch
            {
                "1-1" => "Rana Mohamed Alaa", "1-2" => "Adham Hassan Khaled",
                "2-1" => "Yasmin Khaled Abdel-Aziz", "2-2" => "Omar Ahmed Abdel-Maksoud",
                "3-1" => "Alaa El-Din Mostafa", "3-2" => "Hana Ibrahim Mahmoud",
                "4-1" => "Samar Gamal El-Deen", "4-2" => "Maged Waleed Hany",
                _ => $"Student TEX {s}",
            },
            i => $"3022525{100 + i:D4}67");

        AddStudentsToProgram("Biomedical Engineering", 2,
            (i, s) => s switch
            {
                "1-1" => "Lamia Ashraf Hassan", "1-2" => "Omar Hany Galal",
                "2-1" => "Nourhan Essam Mohamed", "2-2" => "Seif El-Din Khaled",
                "3-1" => "Shahd Mahmoud Ibrahim", "3-2" => "Amr Abdel-Halim Saad",
                "4-1" => "Farah Waleed Mostafa", "4-2" => "Mohanad Yasser Osman",
                _ => $"Student BME {s}",
            },
            i => $"3022626{100 + i:D4}67");

        AddStudentsToProgram("Communications & Information Engineering", 2,
            (i, s) => s switch
            {
                "1-1" => "Mariam Hossam El-Din", "1-2" => "Abdullah Mohamed Kamel",
                "2-1" => "Sara Ahmed El-Sherif", "2-2" => "Mohamed Nagy Abdel-Monem",
                "3-1" => "Hala Said Ibrahim", "3-2" => "Karim Emad El-Din",
                "4-1" => "Nabila Yasser Farouk", "4-2" => "Youssef Ahmed Abdel-Hamid",
                _ => $"Student CIE {s}",
            },
            i => $"3022727{100 + i:D4}67");

        AddStudentsToProgram("Family & Childhood Institution Management", 2,
            (i, s) => s switch
            {
                "1-1" => "Shimaa Ali Abdel-Aziz", "1-2" => "Moustafa Gamal Rabie",
                "2-1" => "Marwa Hani Mahmoud", "2-2" => "Islam Abdel-Nasser",
                "3-1" => "Aisha Tamer Abdou", "3-2" => "Mohamed Khairy Abdel-Wahab",
                "4-1" => "Rasha Emad Hassan", "4-2" => "Ibrahim Shaaban Mohamed",
                _ => $"Student FC {s}",
            },
            i => $"3022828{100 + i:D4}67");

        AddStudentsToProgram("General Stream", 2,
            (i, s) => s switch
            {
                "1-1" => "Hasnaa Magdy Ahmed", "1-2" => "Alaa El-Din Mohamed",
                "2-1" => "Ebtisam Khaled Hassan", "2-2" => "Mohamed Reda Ali",
                "3-1" => "Hanan Abdel-Aziz", "3-2" => "Ahmed Sobhy Mahmoud",
                "4-1" => "Safaa Nabil Ibrahim", "4-2" => "Khalid Yasser Omar",
                _ => $"Student GS {s}",
            },
            i => $"3022929{100 + i:D4}67");

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] Students: +{added} added across all programs.");
    }

    private static async Task SeedCourseOfferingsAsync(CoreDbContext context)
    {
        if (await context.Set<CourseOffering>().AnyAsync()) return;

        var courses = await context.Courses.ToListAsync();
        var semesters = await context.Semesters.Include(s => s.AcademicYear).ToListAsync();
        var programs = await context.StructureNodes.Where(n => n.Type == StructureNodeType.Program).ToListAsync();

        var targetSemesters = semesters
            .Where(s => s.AcademicYear!.Name is "2025-2026" or "2024-2025")
            .OrderBy(s => s.AcademicYear!.Name).ThenBy(s => s.Order)
            .ToList();

        var courseOfferingRepo = context.Set<CourseOffering>();
        var added = 0;

        foreach (var semester in targetSemesters)
        {
            foreach (var program in programs.Take(5))
            {
                foreach (var course in courses.Take(8))
                {
                    var offering = new CourseOffering
                    {
                        Id = Guid.NewGuid(),
                        CourseId = course.Id,
                        SemesterId = semester.Id,
                        StructureNodeId = program.Id,
                        SectionCode = "A",
                    };
                    offering.InitializeCapacity(60);
                    offering.Activate();
                    offering.OpenRegistration();
                    courseOfferingRepo.Add(offering);
                    added++;

                    if (course.CreditHours >= 3)
                    {
                        var offering2 = new CourseOffering
                        {
                            Id = Guid.NewGuid(),
                            CourseId = course.Id,
                            SemesterId = semester.Id,
                            StructureNodeId = program.Id,
                            SectionCode = "B",
                        };
                        offering2.InitializeCapacity(45);
                        offering2.Activate();
                        offering2.OpenRegistration();
                        courseOfferingRepo.Add(offering2);
                        added++;
                    }
                }
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] CourseOfferings: {added} created.");
    }

    private static async Task SeedScheduleSlotsAsync(CoreDbContext context)
    {
        if (await context.Set<ScheduleSlot>().AnyAsync()) return;

        var offerings = await context.Set<CourseOffering>().ToListAsync();
        var scheduleRepo = context.Set<ScheduleSlot>();
        var added = 0;

        var timeSlots = new[]
        {
            (DayOfWeek.Sunday,    new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Sunday,    new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Monday,    new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Monday,    new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Tuesday,   new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Tuesday,   new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Wednesday, new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Thursday,  new TimeOnly(8, 0),  new TimeOnly(9, 30)),
        };

        foreach (var offering in offerings.Take(60))
        {
            var (day, start, end) = timeSlots[added % timeSlots.Length];
            var slot = new ScheduleSlot
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = offering.Id,
                DayOfWeek = day,
                Kind = ScheduleSlotKind.Lecture,
                Location = $"Building {((added % 5) + 1)}, Room {100 + (added % 30)}",
                Notes = null,
            };
            slot.SetTimeRange(start, end);
            scheduleRepo.Add(slot);
            added++;

            if (added % 3 == 0)
            {
                var labSlot = new ScheduleSlot
                {
                    Id = Guid.NewGuid(),
                    CourseOfferingId = offering.Id,
                    DayOfWeek = day == DayOfWeek.Thursday ? DayOfWeek.Wednesday : day + 1,
                    Kind = ScheduleSlotKind.Lab,
                    Location = $"Lab {200 + (added % 20)}",
                    Notes = "Lab session",
                };
                labSlot.SetTimeRange(new TimeOnly(12, 0), new TimeOnly(13, 30));
                scheduleRepo.Add(labSlot);
                added++;
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] ScheduleSlots: {added} created.");
    }

    //private static async Task SeedWorkflowsAsync(CoreDbContext context)
    //{
    //    if (await context.Set<WorkflowDefinition>().AnyAsync()) return;
    //    var workflowRepo = context.Set<WorkflowDefinition>();
    //    var simpleWf = new WorkflowDefinition
    //    {
    //        Id = Guid.NewGuid(),
    //        Code = "simple-approval",
    //        Name = LocalizedJson.Of("اعتماد بسيط", "Simple Approval"),
    //        Description = LocalizedJson.Of("سير عمل بسيط: تقديم → مراجعة → إكمال", "Simple workflow: Submit → Review → Complete"),
    //    };
    //    simpleWf.States = new List<WorkflowState>
    //    {
    //        new() { WorkflowDefinitionId = simpleWf.Id, Status = ServiceRequestStatus.Draft,       DisplayOrder = 0, IsInitial = true,  IsTerminal = false },
    //        new() { WorkflowDefinitionId = simpleWf.Id, Status = ServiceRequestStatus.Submitted,   DisplayOrder = 1, IsInitial = false, IsTerminal = false },
    //        new() { WorkflowDefinitionId = simpleWf.Id, Status = ServiceRequestStatus.UnderReview, DisplayOrder = 2, IsInitial = false, IsTerminal = false },
    //        new() { WorkflowDefinitionId = simpleWf.Id, Status = ServiceRequestStatus.Approved,    DisplayOrder = 3, IsInitial = false, IsTerminal = false },
    //        new() { WorkflowDefinitionId = simpleWf.Id, Status = ServiceRequestStatus.Completed,   DisplayOrder = 4, IsInitial = false, IsTerminal = true  },
    //        new() { WorkflowDefinitionId = simpleWf.Id, Status = ServiceRequestStatus.Rejected,    DisplayOrder = 5, IsInitial = false, IsTerminal = true  },
    //    };
    //    simpleWf.Transitions = new List<WorkflowTransition>
    //    {
    //        new() { WorkflowDefinitionId = simpleWf.Id, FromStatus = ServiceRequestStatus.Draft,       ToStatus = ServiceRequestStatus.Submitted,   TransitionType = WorkflowTransitionType.Student,   RequiredAction = "submit" },
    //        new() { WorkflowDefinitionId = simpleWf.Id, FromStatus = ServiceRequestStatus.Submitted,   ToStatus = ServiceRequestStatus.UnderReview, TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "" },
    //        new() { WorkflowDefinitionId = simpleWf.Id, FromStatus = ServiceRequestStatus.UnderReview, ToStatus = ServiceRequestStatus.Approved,    TransitionType = WorkflowTransitionType.Manual,    RequiredAction = "approve" },
    //        new() { WorkflowDefinitionId = simpleWf.Id, FromStatus = ServiceRequestStatus.UnderReview, ToStatus = ServiceRequestStatus.Rejected,    TransitionType = WorkflowTransitionType.Manual,    RequiredAction = "reject" },
    //        new() { WorkflowDefinitionId = simpleWf.Id, FromStatus = ServiceRequestStatus.Approved,    ToStatus = ServiceRequestStatus.Completed,   TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "" },
    //        new() { WorkflowDefinitionId = simpleWf.Id, FromStatus = ServiceRequestStatus.Draft,       ToStatus = ServiceRequestStatus.Cancelled,    TransitionType = WorkflowTransitionType.Student,   RequiredAction = "cancel" },
    //    };
    //    workflowRepo.Add(simpleWf);
    //    var paymentWf = new WorkflowDefinition
    //    {
    //        Id = Guid.NewGuid(),
    //        Code = "payment-approval",
    //        Name = LocalizedJson.Of("اعتماد مع دفع", "Approval with Payment"),
    //        Description = LocalizedJson.Of("سير عمل مع خطوة دفع: تقديم → دفع → مراجعة → إكمال", "Workflow with payment: Submit → Pay → Review → Complete"),
    //    };
    //    paymentWf.States = new List<WorkflowState>
    //    {
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.Draft,          DisplayOrder = 0, IsInitial = true,  IsTerminal = false, IsWaitingPayment = false },
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.Submitted,      DisplayOrder = 1, IsInitial = false, IsTerminal = false, IsWaitingPayment = false },
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.WaitingPayment, DisplayOrder = 2, IsInitial = false, IsTerminal = false, IsWaitingPayment = true  },
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.UnderReview,    DisplayOrder = 3, IsInitial = false, IsTerminal = false, IsWaitingPayment = false },
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.Approved,       DisplayOrder = 4, IsInitial = false, IsTerminal = false, IsWaitingPayment = false },
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.Completed,      DisplayOrder = 5, IsInitial = false, IsTerminal = true,  IsWaitingPayment = false },
    //        new() { WorkflowDefinitionId = paymentWf.Id, Status = ServiceRequestStatus.Rejected,       DisplayOrder = 6, IsInitial = false, IsTerminal = true,  IsWaitingPayment = false },
    //    };
    //    paymentWf.Transitions = new List<WorkflowTransition>
    //    {
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.Draft,          ToStatus = ServiceRequestStatus.Submitted,      TransitionType = WorkflowTransitionType.Student,   RequiredAction = "submit" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.Submitted,      ToStatus = ServiceRequestStatus.WaitingPayment, TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.WaitingPayment, ToStatus = ServiceRequestStatus.UnderReview,    TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "payment-confirmed" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.UnderReview,    ToStatus = ServiceRequestStatus.Approved,       TransitionType = WorkflowTransitionType.Manual,    RequiredAction = "approve" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.UnderReview,    ToStatus = ServiceRequestStatus.Rejected,       TransitionType = WorkflowTransitionType.Manual,    RequiredAction = "reject" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.Approved,       ToStatus = ServiceRequestStatus.Completed,      TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.Draft,          ToStatus = ServiceRequestStatus.Cancelled,       TransitionType = WorkflowTransitionType.Student,   RequiredAction = "cancel" },
    //        new() { WorkflowDefinitionId = paymentWf.Id, FromStatus = ServiceRequestStatus.WaitingPayment, ToStatus = ServiceRequestStatus.Cancelled,       TransitionType = WorkflowTransitionType.Student,   RequiredAction = "cancel" },
    //    };
    //    workflowRepo.Add(paymentWf);
    //    var multiStepWf = new WorkflowDefinition
    //    {
    //        Id = Guid.NewGuid(),
    //        Code = "multi-step",
    //        Name = LocalizedJson.Of("اعتماد متعدد الخطوات", "Multi-Step Approval"),
    //        Description = LocalizedJson.Of("سير عمل متعدد: تقديم → مراجعة قسم → مراجعة إدارة → اعتماد → إكمال", "Multi-step: Submit → Dept Review → Admin Review → Approve → Complete"),
    //    };
    //    multiStepWf.States = new List<WorkflowState>
    //    {
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.Draft,       DisplayOrder = 0, IsInitial = true,  IsTerminal = false },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.Submitted,   DisplayOrder = 1, IsInitial = false, IsTerminal = false },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.UnderReview, DisplayOrder = 2, IsInitial = false, IsTerminal = false },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.Approved,    DisplayOrder = 3, IsInitial = false, IsTerminal = false },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.Completed,   DisplayOrder = 4, IsInitial = false, IsTerminal = true  },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.Rejected,    DisplayOrder = 5, IsInitial = false, IsTerminal = true  },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, Status = ServiceRequestStatus.Cancelled,   DisplayOrder = 6, IsInitial = false, IsTerminal = true  },
    //    };
    //    multiStepWf.Transitions = new List<WorkflowTransition>
    //    {
    //        new() { WorkflowDefinitionId = multiStepWf.Id, FromStatus = ServiceRequestStatus.Draft,       ToStatus = ServiceRequestStatus.Submitted,   TransitionType = WorkflowTransitionType.Student,   RequiredAction = "submit" },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, FromStatus = ServiceRequestStatus.Submitted,   ToStatus = ServiceRequestStatus.UnderReview, TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "" },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, FromStatus = ServiceRequestStatus.UnderReview, ToStatus = ServiceRequestStatus.Approved,    TransitionType = WorkflowTransitionType.Manual,    RequiredAction = "approve" },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, FromStatus = ServiceRequestStatus.UnderReview, ToStatus = ServiceRequestStatus.Rejected,    TransitionType = WorkflowTransitionType.Manual,    RequiredAction = "reject" },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, FromStatus = ServiceRequestStatus.Approved,    ToStatus = ServiceRequestStatus.Completed,   TransitionType = WorkflowTransitionType.Automatic, RequiredAction = "" },
    //        new() { WorkflowDefinitionId = multiStepWf.Id, FromStatus = ServiceRequestStatus.Draft,       ToStatus = ServiceRequestStatus.Cancelled,    TransitionType = WorkflowTransitionType.Student,   RequiredAction = "cancel" },
    //    };
    //    workflowRepo.Add(multiStepWf);
    //    await context.SaveChangesAsync();
    //    Console.WriteLine($"[MassSeed] Workflows: 3 definitions created.");
    //}

    //private static async Task SeedStudentServicesAsync(CoreDbContext context)
    //{
    //    if (await context.Set<StudentService>().AnyAsync()) return;
    //    var workflows = await context.Set<WorkflowDefinition>().ToListAsync();
    //    var simpleWfId = workflows.First(w => w.Code == "simple-approval").Id;
    //    var paymentWfId = workflows.First(w => w.Code == "payment-approval").Id;
    //    var multiWfId = workflows.First(w => w.Code == "multi-step").Id;
    //    var roles = await context.Roles.ToListAsync();
    //    var registrarRoleId = roles.First(r => LocalizedJson.Extract(r.Name, "en") == "Registrar").Id;
    //    var facAdminRoleId = roles.First(r => LocalizedJson.Extract(r.Name, "en") == "Faculty Admin").Id;
    //    var deptHeadRoleId = roles.First(r => LocalizedJson.Extract(r.Name, "en") == "Department Head").Id;
    //    var staffRoleId = roles.First(r => LocalizedJson.Extract(r.Name, "en") == "Staff").Id;
    //    var serviceRepo = context.Set<StudentService>();
    //    var transcriptService = new StudentService
    //    {
    //        Id = Guid.NewGuid(),
    //        Code = "transcript-request",
    //        Name = LocalizedJson.Of("طلب بيان درجات", "Transcript Request"),
    //        Description = LocalizedJson.Of("طلب الحصول على بيان درجات رسمي معتمد من الكلية", "Request an official certified transcript from the faculty"),
    //        IsActive = true, RequiresPayment = true, FeeType = "خدمة بيان درجات", FeeAmount = 150.00m, Currency = "EGP",
    //        EstimatedProcessingDays = 5, AllowedProcessingRoleIdsCsv = $"{registrarRoleId},{facAdminRoleId}", WorkflowDefinitionId = paymentWfId,
    //    };
    //    transcriptService.Fields = new List<ServiceFieldDefinition>
    //    {
    //        new() { StudentServiceId = transcriptService.Id, Name = "full_name", Label = LocalizedJson.Of("الاسم الكامل", "Full Name"), FieldType = DynamicFieldType.Text, IsRequired = true, DisplayOrder = 0, MinLength = 5, MaxLength = 100 },
    //        new() { StudentServiceId = transcriptService.Id, Name = "student_id", Label = LocalizedJson.Of("الرقم الجامعي", "Student ID"), FieldType = DynamicFieldType.Text, IsRequired = true, DisplayOrder = 1, MinLength = 8, MaxLength = 20 },
    //        new() { StudentServiceId = transcriptService.Id, Name = "copies", Label = LocalizedJson.Of("عدد النسخ", "Number of Copies"), FieldType = DynamicFieldType.Number, IsRequired = true, DisplayOrder = 2, MinValue = 1, MaxValue = 10 },
    //        new() { StudentServiceId = transcriptService.Id, Name = "language", Label = LocalizedJson.Of("اللغة", "Language"), FieldType = DynamicFieldType.Dropdown, IsRequired = true, DisplayOrder = 3, DropdownValues = "arabic,english,both" },
    //        new() { StudentServiceId = transcriptService.Id, Name = "delivery_method", Label = LocalizedJson.Of("طريقة الاستلام", "Delivery Method"), FieldType = DynamicFieldType.Dropdown, IsRequired = true, DisplayOrder = 4, DropdownValues = "in_person,mail,courier" },
    //        new() { StudentServiceId = transcriptService.Id, Name = "purpose", Label = LocalizedJson.Of("الغرض من الطلب", "Purpose of Request"), FieldType = DynamicFieldType.MultilineText, IsRequired = false, DisplayOrder = 5, MaxLength = 500 },
    //    };
    //    transcriptService.Documents = new List<ServiceDocumentDefinition>
    //    {
    //        new() { StudentServiceId = transcriptService.Id, Name = "id_copy", Label = LocalizedJson.Of("صورة بطاقة الرقم القومي", "National ID Copy"), IsRequired = true, DisplayOrder = 0, AllowedExtensions = "pdf,jpg,png", MaxFileSizeBytes = 5_242_880 },
    //        new() { StudentServiceId = transcriptService.Id, Name = "payment_receipt", Label = LocalizedJson.Of("إيصال الدفع", "Payment Receipt"), IsRequired = false, DisplayOrder = 1, AllowedExtensions = "pdf,jpg,png", MaxFileSizeBytes = 5_242_880 },
    //    };
    //    serviceRepo.Add(transcriptService);
    //    var enrollmentCert = new StudentService
    //    {
    //        Id = Guid.NewGuid(), Code = "enrollment-certificate",
    //        Name = LocalizedJson.Of("شهادة قيد", "Enrollment Certificate"),
    //        Description = LocalizedJson.Of("طلب شهادة قيد رسمية للطلبة المسجلين", "Request an official enrollment certificate for registered students"),
    //        IsActive = true, RequiresPayment = true, FeeType = "خدمة شهادة قيد", FeeAmount = 75.00m, Currency = "EGP",
    //        EstimatedProcessingDays = 3, AllowedProcessingRoleIdsCsv = $"{registrarRoleId},{staffRoleId}", WorkflowDefinitionId = paymentWfId,
    //    };
    //    enrollmentCert.Fields = new List<ServiceFieldDefinition>
    //    {
    //        new() { StudentServiceId = enrollmentCert.Id, Name = "full_name", Label = LocalizedJson.Of("الاسم الكامل", "Full Name"), FieldType = DynamicFieldType.Text, IsRequired = true, DisplayOrder = 0, MinLength = 5, MaxLength = 100 },
    //        new() { StudentServiceId = enrollmentCert.Id, Name = "student_id", Label = LocalizedJson.Of("الرقم الجامعي", "Student ID"), FieldType = DynamicFieldType.Text, IsRequired = true, DisplayOrder = 1, MinLength = 8, MaxLength = 20 },
    //        new() { StudentServiceId = enrollmentCert.Id, Name = "graduation_year", Label = LocalizedJson.Of("السنة الدراسية", "Academic Year"), FieldType = DynamicFieldType.Text, IsRequired = true, DisplayOrder = 2, MinLength = 4, MaxLength = 20 },
    //        new() { StudentServiceId = enrollmentCert.Id, Name = "language", Label = LocalizedJson.Of("اللغة", "Language"), FieldType = DynamicFieldType.Dropdown, IsRequired = true, DisplayOrder = 3, DropdownValues = "arabic,english,both" },
    //        new() { StudentServiceId = enrollmentCert.Id, Name = "delivery_method", Label = LocalizedJson.Of("طريقة الاستلام", "Delivery Method"), FieldType = DynamicFieldType.Dropdown, IsRequired = true, DisplayOrder = 4, DropdownValues = "in_person,mail,courier" },
    //    };
    //    enrollmentCert.Documents = new List<ServiceDocumentDefinition>
    //    {
    //        new() { StudentServiceId = enrollmentCert.Id, Name = "id_copy", Label = LocalizedJson.Of("صورة بطاقة الرقم القومي", "National ID Copy"), IsRequired = true, DisplayOrder = 0, AllowedExtensions = "pdf,jpg,png", MaxFileSizeBytes = 5_242_880 },
    //    };
    //    serviceRepo.Add(enrollmentCert);
    //    var simpleServices = new[]
    //    {
    //        new { Code = "leave-request", NameAr = "طلب إجازة", NameEn = "Leave Request", Fee = 0m, Days = 7 },
    //        new { Code = "grievance", NameAr = "تقديم تظلم", NameEn = "Submit Grievance", Fee = 0m, Days = 14 },
    //        new { Code = "appeal", NameAr = "استئناف قرار", NameEn = "Appeal Decision", Fee = 0m, Days = 30 },
    //        new { Code = "name-change", NameAr = "طلب تغيير الاسم", NameEn = "Name Change Request", Fee = 100m, Days = 10 },
    //        new { Code = "grade-review", NameAr = "مراجعة درجة", NameEn = "Grade Review Request", Fee = 50m, Days = 7 },
    //        new { Code = "document-attestation", NameAr = "تصديق مستند", NameEn = "Document Attestation", Fee = 25m, Days = 3 },
    //    };
    //    foreach (var svc in simpleServices)
    //    {
    //        var service = new StudentService
    //        {
    //            Id = Guid.NewGuid(), Code = svc.Code,
    //            Name = LocalizedJson.Of(svc.NameAr, svc.NameEn),
    //            Description = LocalizedJson.Of($"طلب {svc.NameAr}", $"{svc.NameEn} request"),
    //            IsActive = true, RequiresPayment = svc.Fee > 0, FeeType = svc.Fee > 0 ? $"خدمة {svc.NameAr}" : null,
    //            FeeAmount = svc.Fee > 0 ? svc.Fee : null, Currency = "EGP", EstimatedProcessingDays = svc.Days,
    //            AllowedProcessingRoleIdsCsv = $"{registrarRoleId},{facAdminRoleId},{deptHeadRoleId}",
    //            WorkflowDefinitionId = svc.Fee > 0 ? paymentWfId : simpleWfId,
    //        };
    //        service.Fields = new List<ServiceFieldDefinition>
    //        {
    //            new() { StudentServiceId = service.Id, Name = "student_id", Label = LocalizedJson.Of("الرقم الجامعي", "Student ID"), FieldType = DynamicFieldType.Text, IsRequired = true, DisplayOrder = 0, MinLength = 8, MaxLength = 20 },
    //            new() { StudentServiceId = service.Id, Name = "description", Label = LocalizedJson.Of("الوصف", "Description"), FieldType = DynamicFieldType.MultilineText, IsRequired = true, DisplayOrder = 1, MaxLength = 1000 },
    //        };
    //        service.Documents = new List<ServiceDocumentDefinition>
    //        {
    //            new() { StudentServiceId = service.Id, Name = "supporting_doc", Label = LocalizedJson.Of("مستند داعم", "Supporting Document"), IsRequired = false, DisplayOrder = 0, AllowedExtensions = "pdf,jpg,png", MaxFileSizeBytes = 5_242_880 },
    //        };
    //        serviceRepo.Add(service);
    //    }
    //    await context.SaveChangesAsync();
    //    Console.WriteLine($"[MassSeed] StudentServices: 8 services created.");
    //}

    //private static async Task SeedStudentServiceRequestsAsync(CoreDbContext context)
    //{
    //    if (await context.Set<StudentServiceRequest>().AnyAsync()) return;
    //
    //    var services = await context.Set<StudentService>().ToListAsync();
    //    var students = await context.Students.Take(20).ToListAsync();
    //    var staff = await context.Staffs.Take(3).ToListAsync();
    //
    //    var transcriptServiceId = services.First(s => s.Code == "transcript-request").Id;
    //    var enrollmentServiceId = services.First(s => s.Code == "enrollment-certificate").Id;
    //    var leaveServiceId = services.First(s => s.Code == "leave-of-absence").Id;
    //    var grievanceServiceId = services.First(s => s.Code == "academic-grievance").Id;
    //    var idCardServiceId = services.First(s => s.Code == "student-id-card").Id;
    //    var withdrawalServiceId = services.First(s => s.Code == "course-withdrawal").Id;
    //    var graduationServiceId = services.First(s => s.Code == "graduation-application").Id;
    //
    //    var requestRepo = context.Set<StudentServiceRequest>();
    //    var now = DateTime.UtcNow;
    //
    //    StudentServiceRequest MakeRequest(Guid studentId, Guid serviceId, ServiceRequestStatus status,
    //        Guid? staffId, DateTime? submitted, DateTime? processed, (string Name, string Value)[] fields)
    //    {
    //        var req = new StudentServiceRequest
    //        {
    //            Id = Guid.NewGuid(),
    //            StudentId = studentId,
    //            StudentServiceId = serviceId,
    //            CurrentStatus = status,
    //            SubmittedAt = submitted,
    //            ProcessedAt = processed,
    //            AssignedStaffId = staffId,
    //            CreatedAt = submitted ?? now,
    //        };
    //
    //        foreach (var (name, value) in fields)
    //        {
    //            req.FieldValues.Add(new ServiceFieldValue
    //            {
    //                StudentServiceRequestId = req.Id,
    //                FieldDefinitionId = services.First(s => s.Id == serviceId).Fields
    //                    .FirstOrDefault(f => f.Name == name)?.Id ?? Guid.NewGuid(),
    //                Value = value,
    //            });
    //        }
    //
    //        return req;
    //    }
    //
    //    requestRepo.Add(MakeRequest(students[0].Id, transcriptServiceId, ServiceRequestStatus.Completed,
    //        staff[0].Id,
    //        now.AddDays(-10), now.AddDays(-5),
    //        new[] {
    //            ("full_name", "Ahmed Mohamed Ali"),
    //            ("student_id", "20250001"),
    //            ("copies", "3"),
    //            ("language", "english"),
    //            ("delivery_method", "in_person"),
    //            ("purpose", "Graduate school application"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[1].Id, enrollmentServiceId, ServiceRequestStatus.UnderReview,
    //        staff[1].Id,
    //        now.AddDays(-3), null,
    //        new[] {
    //            ("full_name", "Sara Mahmoud Hassan"),
    //            ("student_id", "20250002"),
    //            ("certificate_type", "stamped"),
    //            ("quantity", "2"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[2].Id, leaveServiceId, ServiceRequestStatus.Submitted,
    //        null,
    //        now.AddDays(-1), null,
    //        new[] {
    //            ("full_name", "Mohamed Khaled Ibrahim"),
    //            ("student_id", "20250003"),
    //            ("leave_type", "medical"),
    //            ("semester", "spring"),
    //            ("academic_year", "2025-2026"),
    //            ("reason", "Medical treatment requiring extended recovery period as per doctor's recommendation."),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[3].Id, grievanceServiceId, ServiceRequestStatus.Approved,
    //        staff[2].Id,
    //        now.AddDays(-7), now.AddDays(-4),
    //        new[] {
    //            ("full_name", "Nourhan Atef El-Sayed"),
    //            ("student_id", "20250004"),
    //            ("course_code", "MATH101"),
    //            ("exam_type", "final"),
    //            ("grievance_reason", "grading_error"),
    //            ("details", "Requesting re-evaluation of Calculus I final exam. The total appears miscalculated."),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[4].Id, idCardServiceId, ServiceRequestStatus.Draft,
    //        null, null, null,
    //        new[] {
    //            ("full_name", "Mariam Tarek Fathy"),
    //            ("student_id", "20250005"),
    //            ("card_type", "replacement"),
    //            ("reason", "lost"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[5].Id, withdrawalServiceId, ServiceRequestStatus.Rejected,
    //        staff[0].Id,
    //        now.AddDays(-15), now.AddDays(-12),
    //        new[] {
    //            ("full_name", "Youssef Gamal El-Din"),
    //            ("student_id", "20250006"),
    //            ("course_code", "TEX201"),
    //            ("reason", "other"),
    //            ("details", "Withdrawal period has passed."),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[6].Id, enrollmentServiceId, ServiceRequestStatus.Completed,
    //        staff[1].Id,
    //        now.AddDays(-20), now.AddDays(-18),
    //        new[] {
    //            ("full_name", "Omar Hossam El-Din"),
    //            ("student_id", "20250007"),
    //            ("certificate_type", "regular"),
    //            ("quantity", "1"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[7].Id, transcriptServiceId, ServiceRequestStatus.WaitingPayment,
    //        null,
    //        now.AddDays(-2), null,
    //        new[] {
    //            ("full_name", "Laila Sherif Kamal"),
    //            ("student_id", "20250008"),
    //            ("copies", "1"),
    //            ("language", "arabic"),
    //            ("delivery_method", "in_person"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[8].Id, leaveServiceId, ServiceRequestStatus.Cancelled,
    //        null,
    //        now.AddDays(-25), now.AddDays(-24),
    //        new[] {
    //            ("full_name", "Ali Hassan Mohamed"),
    //            ("student_id", "20250009"),
    //            ("leave_type", "semester"),
    //            ("semester", "fall"),
    //            ("academic_year", "2025-2026"),
    //            ("reason", "No longer needed."),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[9].Id, grievanceServiceId, ServiceRequestStatus.Submitted,
    //        null,
    //        now.AddHours(-6), null,
    //        new[] {
    //            ("full_name", "Hagar Mahmoud Ahmed"),
    //            ("student_id", "20250010"),
    //            ("course_code", "CS201"),
    //            ("exam_type", "final"),
    //            ("grievance_reason", "missing_result"),
    //            ("details", "My Data Structures final exam result is not showing on the portal."),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[10].Id, idCardServiceId, ServiceRequestStatus.Completed,
    //        staff[2].Id,
    //        now.AddDays(-30), now.AddDays(-28),
    //        new[] {
    //            ("full_name", "Karim Mostafa Abdel-Aziz"),
    //            ("student_id", "20250011"),
    //            ("card_type", "new"),
    //            ("reason", "first_time"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[11].Id, graduationServiceId, ServiceRequestStatus.UnderReview,
    //        staff[0].Id,
    //        now.AddDays(-5), null,
    //        new[] {
    //            ("full_name", "Salma Adel Naguib"),
    //            ("student_id", "20250012"),
    //            ("graduation_semester", "spring"),
    //            ("graduation_year", "2025-2026"),
    //            ("honors", "true"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[12].Id, withdrawalServiceId, ServiceRequestStatus.Approved,
    //        staff[1].Id,
    //        now.AddDays(-8), now.AddDays(-6),
    //        new[] {
    //            ("full_name", "Hassan Emad El-Din"),
    //            ("student_id", "20250013"),
    //            ("course_code", "GEN150"),
    //            ("section", "A"),
    //            ("reason", "schedule_conflict"),
    //            ("details", "Public Speaking clashes with my lab session."),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[13].Id, transcriptServiceId, ServiceRequestStatus.Rejected,
    //        staff[0].Id,
    //        now.AddDays(-12), now.AddDays(-10),
    //        new[] {
    //            ("full_name", "Dalia Samir Fawzy"),
    //            ("student_id", "20250014"),
    //            ("copies", "5"),
    //            ("language", "both"),
    //            ("delivery_method", "courier"),
    //            ("purpose", "Multiple university applications"),
    //        }));
    //
    //    requestRepo.Add(MakeRequest(students[14].Id, leaveServiceId, ServiceRequestStatus.Draft,
    //        null, null, null,
    //        new[] {
    //            ("full_name", "Amr Khaled Youssef"),
    //            ("student_id", "20250015"),
    //            ("leave_type", "full_year"),
    //            ("semester", "fall"),
    //            ("academic_year", "2026-2027"),
    //            ("reason", "Planning to take a gap year for professional internship."),
    //        }));
    //
    //    await context.SaveChangesAsync();
    //    Console.WriteLine($"[MassSeed] StudentServiceRequests: 15 requests created.");
    //}

    private static async Task ExpandPaymentsAsync(CoreDbContext context)
    {
        if (await context.Set<Invoice>().CountAsync() > 10) return;

        var students = await context.Students.OrderBy(s => s.StudentCode).Take(15).ToListAsync();
        if (students.Count < 5) return;

        var invoiceRepo = context.Set<Invoice>();
        var now = DateTime.UtcNow;

        var saraInv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[1].Id,
            TotalAmount = 37_500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Paid,
            DueAt = new DateTime(2025, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        saraInv.Items.Add(new InvoiceItem
        {
            InvoiceId = saraInv.Id,
            Amount = 37_500.00m,
            FeeType = "مصروفات دراسية",
            SourceModule = "registration",
            Description = "مصاريف الترم الأول - 2025 - الفرقة الرابعة",
        });
        saraInv.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = saraInv.Id,
            Provider = "Bank Transfer",
            ProviderTransactionId = "BNK-20250925-002",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 37_500.00m,
            IdempotencyKey = "idem-sarainv1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2025, 9, 25, 10, 30, 0, DateTimeKind.Utc),
        });
        invoiceRepo.Add(saraInv);

        var saraInv2 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[1].Id,
            TotalAmount = 500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            CreatedAt = now.AddDays(-30),
        };
        saraInv2.Items.Add(new InvoiceItem
        {
            InvoiceId = saraInv2.Id,
            Amount = 500.00m,
            FeeType = "مصروفات إدارية",
            SourceModule = "admin",
            Description = "الأنشطة الطلابية - 2025",
        });
        invoiceRepo.Add(saraInv2);

        var omarInv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[6].Id,
            TotalAmount = 18_750.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Paid,
            DueAt = new DateTime(2025, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        omarInv.Items.Add(new InvoiceItem
        {
            InvoiceId = omarInv.Id,
            Amount = 18_750.00m,
            FeeType = "مصروفات دراسية",
            SourceModule = "registration",
            Description = "مصاريف الترم الأول - 2025 - الهندسة المدنية",
        });
        omarInv.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = omarInv.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20251001-003",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 18_750.00m,
            IdempotencyKey = "idem-omarinv1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        });
        invoiceRepo.Add(omarInv);

        var aliInv1 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[8].Id,
            TotalAmount = 37_500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Refunded,
            CreatedAt = new DateTime(2025, 8, 15, 8, 0, 0, DateTimeKind.Utc),
        };
        aliInv1.Items.Add(new InvoiceItem
        {
            InvoiceId = aliInv1.Id,
            Amount = 37_500.00m,
            FeeType = "مصروفات دراسية",
            SourceModule = "registration",
            Description = "مصاريف الترم الأول - 2025 (ملغية)",
        });
        aliInv1.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = aliInv1.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20250915-004",
            Status = PaymentTransactionStatus.Refunded,
            Amount = 37_500.00m,
            IdempotencyKey = "idem-aliinv1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2025, 9, 15, 14, 0, 0, DateTimeKind.Utc),
        });
        invoiceRepo.Add(aliInv1);

        var aliInv2 = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[8].Id,
            TotalAmount = 3_400.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            DueAt = now.AddDays(30),
            CreatedAt = now.AddDays(-10),
        };
        aliInv2.Items.Add(new InvoiceItem
        {
            InvoiceId = aliInv2.Id,
            Amount = 3_400.00m,
            FeeType = "خدمات",
            SourceModule = "services",
            Description = "رسوم معادلة مواد",
        });
        invoiceRepo.Add(aliInv2);

        var hagarInv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[9].Id,
            TotalAmount = 37_500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.PartiallyPaid,
            DueAt = new DateTime(2025, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
        };
        hagarInv.Items.Add(new InvoiceItem
        {
            InvoiceId = hagarInv.Id,
            Amount = 37_500.00m,
            FeeType = "مصروفات دراسية",
            SourceModule = "registration",
            Description = "مصاريف الترم الأول - 2025",
        });
        hagarInv.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = hagarInv.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20250920-005",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 15_000.00m,
            IdempotencyKey = "idem-hagar1",
            RawPayloadJson = "{}",
            CreatedAt = new DateTime(2025, 9, 20, 9, 15, 0, DateTimeKind.Utc),
        });
        invoiceRepo.Add(hagarInv);

        var karimInv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[10].Id,
            TotalAmount = 150.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Paid,
            DueAt = now.AddDays(-5),
            CreatedAt = now.AddDays(-10),
        };
        karimInv.Items.Add(new InvoiceItem
        {
            InvoiceId = karimInv.Id,
            Amount = 150.00m,
            FeeType = "خدمة بيان درجات",
            SourceModule = "student_services",
            Description = "رسوم طلب بيان درجات",
        });
        karimInv.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = karimInv.Id,
            Provider = "Fawry",
            ProviderTransactionId = "FAW-20260601-006",
            Status = PaymentTransactionStatus.Succeeded,
            Amount = 150.00m,
            IdempotencyKey = "idem-karim-transcript",
            RawPayloadJson = "{}",
            CreatedAt = now.AddDays(-5),
        });
        invoiceRepo.Add(karimInv);

        var salmaInv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[11].Id,
            TotalAmount = 500.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            DueAt = now.AddDays(14),
            CreatedAt = now.AddDays(-30),
        };
        salmaInv.Items.Add(new InvoiceItem
        {
            InvoiceId = salmaInv.Id,
            Amount = 500.00m,
            FeeType = "رسوم تخرج",
            SourceModule = "student_services",
            Description = "رسوم التخرج واصدار الشهادة",
        });
        invoiceRepo.Add(salmaInv);

        var hassanInv = new Invoice
        {
            Id = Guid.NewGuid(),
            StudentId = students[12].Id,
            TotalAmount = 85.00m,
            Currency = "EGP",
            Status = InvoiceStatus.Pending,
            CreatedAt = now.AddDays(-3),
        };
        hassanInv.Items.Add(new InvoiceItem
        {
            InvoiceId = hassanInv.Id,
            Amount = 85.00m,
            FeeType = "خدمة كارنية طالب",
            SourceModule = "student_services",
            Description = "رسوم إصدار كارنية طالب",
        });
        hassanInv.Transactions.Add(new PaymentTransaction
        {
            InvoiceId = hassanInv.Id,
            Provider = "Online",
            ProviderTransactionId = "TXN-20260604-007",
            Status = PaymentTransactionStatus.Failed,
            Amount = 85.00m,
            IdempotencyKey = "idem-hassan-idcard-fail",
            RawPayloadJson = "{\"error\":\"insufficient_funds\"}",
            CreatedAt = now.AddDays(-3),
        });
        invoiceRepo.Add(hassanInv);

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] Payments: expanded with multi-student invoices.");
    }

    private static async Task SeedStudentProfileRecordsAsync(CoreDbContext context)
    {
        if (await context.Set<StudentProfileRecord>().AnyAsync()) return;

        var students = await context.Students.OrderBy(s => s.StudentCode).Take(20).ToListAsync();
        var staff = await context.Staffs.Take(2).ToListAsync();

        var recordRepo = context.Set<StudentProfileRecord>();

        var maleStudents = students.Where(s => s.Name.Contains("Mohamed") || s.Name.Contains("Ahmed")
            || s.Name.Contains("Ali") || s.Name.Contains("Omar") || s.Name.Contains("Youssef")
            || s.Name.Contains("Karim") || s.Name.Contains("Hassan") || s.Name.Contains("Amr")
            || s.Name.Contains("Mostafa") || s.Name.Contains("Khaled") || s.Name.Contains("Hossam")
            || s.Name.Contains("Yassin") || s.Name.Contains("Tamer") || s.Name.Contains("Islam")
            || s.Name.Contains("Mahmoud") || s.Name.Contains("Ibrahim") || s.Name.Contains("Khalid")
            || s.Name.Contains("Adham") || s.Name.Contains("Maged") || s.Name.Contains("Seif"))
            .Take(8).ToList();

        foreach (var student in maleStudents)
        {
            recordRepo.Add(new StudentProfileRecord
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                Category = StudentProfileCategory.MilitaryInformation,
                SchemaVersion = 1,
                DataJson = $@"{{
                    ""status"": ""postponed"",
                    ""postponement_reason"": ""studies"",
                    ""postponement_until"": ""{DateTime.UtcNow.Year + 4}"",
                    ""military_id"": ""MIL-{student.StudentCode}"",
                    ""registration_office"": ""{new[] {"Cairo", "Giza", "Alexandria"}[(student.StudentCode.GetHashCode() & int.MaxValue) % 3]}""
                }}",
                IsSensitive = true,
            });
        }

        foreach (var student in students.Take(15))
        {
            recordRepo.Add(new StudentProfileRecord
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                Category = StudentProfileCategory.VaccinationInformation,
                SchemaVersion = 1,
                DataJson = $@"{{
                    ""covid_vaccinated"": true,
                    ""covid_doses"": 3,
                    ""last_vaccination_date"": ""2024-09-15"",
                    ""vaccine_type"": ""{((student.StudentCode.GetHashCode() & int.MaxValue) % 2 == 0 ? "Sinopharm" : "Pfizer")}"",
                    ""meningitis_vaccinated"": true,
                    ""meningitis_date"": ""{2024 + ((student.StudentCode.GetHashCode() & int.MaxValue) % 2)}-08-{(15 + (student.StudentCode.GetHashCode() & int.MaxValue) % 10):D2}"",
                    ""other_vaccinations"": ""Hepatitis B, Tetanus""
                }}",
            });
        }

        foreach (var student in students.Take(20))
        {
            var contactNames = new[] { "Ahmed", "Mahmoud", "Hassan", "Mostafa", "Khaled", "Tamer" };
            var relationships = new[] { "Father", "Mother", "Brother", "Sister", "Guardian" };
            var rIdx = (student.StudentCode.GetHashCode() & int.MaxValue) % relationships.Length;
            recordRepo.Add(new StudentProfileRecord
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                Category = StudentProfileCategory.EmergencyContact,
                SchemaVersion = 1,
                DataJson = $@"{{
                    ""contact_name"": ""{contactNames[(student.StudentCode.GetHashCode() & int.MaxValue) % contactNames.Length]} {student.Name.Split(' ').LastOrDefault() ?? "Family"}"",
                    ""relationship"": ""{relationships[rIdx]}"",
                    ""phone"": ""0120{100 + (student.StudentCode.GetHashCode() & int.MaxValue) % 900:D3}{100 + ((student.StudentCode.GetHashCode() & int.MaxValue) * 7) % 900:D3}"",
                    ""email"": ""emergency_{student.StudentCode}@family.com"",
                    ""address"": ""{new[] {"Cairo", "Giza", "Helwan", "Shubra", "Maadi", "Nasr City"}[(student.StudentCode.GetHashCode() & int.MaxValue) % 6]}""
                }}",
            });
        }

        if (students.Count >= 5)
        {
            recordRepo.Add(new StudentProfileRecord
            {
                Id = Guid.NewGuid(),
                StudentId = students[4].Id,
                Category = StudentProfileCategory.DisabilityInformation,
                SchemaVersion = 1,
                DataJson = @"{
                    ""has_disability"": true,
                    ""disability_type"": ""visual_impairment"",
                    ""severity"": ""moderate"",
                    ""requires_assistance"": true,
                    ""assistance_type"": ""extended_exam_time, screen_reader"",
                    ""registered_at"": ""2025-09-01""
                }",
                IsSensitive = true,
            });

            recordRepo.Add(new StudentProfileRecord
            {
                Id = Guid.NewGuid(),
                StudentId = students[7].Id,
                Category = StudentProfileCategory.DisabilityInformation,
                SchemaVersion = 1,
                DataJson = @"{
                    ""has_disability"": true,
                    ""disability_type"": ""physical_mobility"",
                    ""severity"": ""mild"",
                    ""requires_assistance"": true,
                    ""assistance_type"": ""ground_floor_classrooms, ramp_access"",
                    ""registered_at"": ""2025-09-01""
                }",
                IsSensitive = true,
            });
        }

        if (students.Count >= 10)
        {
            foreach (var idx in new[] { 2, 6, 9 })
            {
                recordRepo.Add(new StudentProfileRecord
                {
                    Id = Guid.NewGuid(),
                    StudentId = students[idx].Id,
                    Category = StudentProfileCategory.HousingInformation,
                    SchemaVersion = 1,
                    DataJson = @"{
                        ""residence_type"": ""university_housing"",
                        ""dormitory_name"": ""Capital University Student Housing"",
                        ""room_number"": """ + (100 + idx) + @""",
                        ""building"": ""Building " + (char)('A' + idx) + @""",
                        ""check_in_date"": ""2025-09-15"",
                        ""check_out_date"": ""2026-06-30"",
                        ""monthly_fee"": 1200.00,
                        ""meal_plan"": ""half_board""
                    }",
                });
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] StudentProfileRecords: created.");
    }
}
