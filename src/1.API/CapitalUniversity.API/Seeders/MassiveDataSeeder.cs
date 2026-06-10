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
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapitalUniversity.API.Seeders;

public static class MassiveDataSeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        // Courses and AcademicPlans are now seeded by DataSeeder in Core.Infrastructure.
        await ExpandStudentsAsync(context, passwordHasher);
        await SeedCourseOfferingsAsync(context);
        await SeedScheduleSlotsAsync(context);
        //await SeedWorkflowsAsync(context);
        //await SeedStudentServicesAsync(context);
        //await SeedStudentServiceRequestsAsync(context);
        await ExpandPaymentsAsync(context);
        await SeedStudentProfileRecordsAsync(context);
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

        var courses = await context.Courses.ToDictionaryAsync(c => c.Id);
        var semesters = await context.Semesters.Include(s => s.AcademicYear).ToListAsync();
        var plans = await context.AcademicPlans.Include(p => p.PlanCourses).ToListAsync();

        var fallSemester = semesters.FirstOrDefault(s =>
            s.AcademicYear!.Name == "2025-2026" && s.Order == 1);
        var springSemester = semesters.FirstOrDefault(s =>
            s.AcademicYear!.Name == "2025-2026" && s.Order == 2);

        if (fallSemester == null && springSemester == null)
        {
            Console.WriteLine($"[MassSeed] CourseOfferings: no 2025-2026 semesters found, skipping.");
            return;
        }

        var courseOfferingRepo = context.Set<CourseOffering>();
        var added = 0;

        foreach (var plan in plans)
        {
            foreach (var planCourse in plan.PlanCourses)
            {
                if (!courses.TryGetValue(planCourse.CourseId, out var course)) continue;

                // Plan semester 1 → Fall, semester 2 → Spring
                var targetSemester = planCourse.Semester == 1 ? fallSemester : springSemester;
                if (targetSemester == null) continue;

                // Section A
                var offering = new CourseOffering
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    SemesterId = targetSemester.Id,
                    StructureNodeId = plan.StructureNodeId,
                    SectionCode = "A",
                };
                offering.InitializeCapacity(60);
                offering.Activate();
                offering.OpenRegistration();
                courseOfferingRepo.Add(offering);
                added++;

                // Section B for courses with >= 3 credit hours
                if (course.CreditHours >= 3)
                {
                    var offeringB = new CourseOffering
                    {
                        Id = Guid.NewGuid(),
                        CourseId = course.Id,
                        SemesterId = targetSemester.Id,
                        StructureNodeId = plan.StructureNodeId,
                        SectionCode = "B",
                    };
                    offeringB.InitializeCapacity(45);
                    offeringB.Activate();
                    offeringB.OpenRegistration();
                    courseOfferingRepo.Add(offeringB);
                    added++;
                }
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] CourseOfferings: {added} created across {plans.Count} plans.");
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
            (DayOfWeek.Sunday,    new TimeOnly(12, 0), new TimeOnly(13, 30)),
            (DayOfWeek.Monday,    new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Monday,    new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Monday,    new TimeOnly(12, 0), new TimeOnly(13, 30)),
            (DayOfWeek.Tuesday,   new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Tuesday,   new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Tuesday,   new TimeOnly(12, 0), new TimeOnly(13, 30)),
            (DayOfWeek.Wednesday, new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Wednesday, new TimeOnly(10, 0), new TimeOnly(11, 30)),
            (DayOfWeek.Wednesday, new TimeOnly(12, 0), new TimeOnly(13, 30)),
            (DayOfWeek.Thursday,  new TimeOnly(8, 0),  new TimeOnly(9, 30)),
            (DayOfWeek.Thursday,  new TimeOnly(10, 0), new TimeOnly(11, 30)),
        };

        var idx = 0;
        foreach (var offering in offerings)
        {
            var (day, start, end) = timeSlots[idx % timeSlots.Length];
            var slot = new ScheduleSlot
            {
                Id = Guid.NewGuid(),
                CourseOfferingId = offering.Id,
                DayOfWeek = day,
                Kind = ScheduleSlotKind.Lecture,
                Location = $"Building {(idx % 8) + 1}, Room {100 + (idx % 40)}",
                Notes = null,
            };
            slot.SetTimeRange(start, end);
            scheduleRepo.Add(slot);
            added++;
            idx++;

            // Lab session for every 3rd offering (alternating days)
            if (idx % 3 == 0)
            {
                var labDay = day == DayOfWeek.Thursday ? DayOfWeek.Wednesday : (DayOfWeek)(((int)day + 2) % 7);
                var labSlot = new ScheduleSlot
                {
                    Id = Guid.NewGuid(),
                    CourseOfferingId = offering.Id,
                    DayOfWeek = labDay,
                    Kind = ScheduleSlotKind.Lab,
                    Location = $"Lab {200 + (idx % 25)}",
                    Notes = "Lab session",
                };
                labSlot.SetTimeRange(new TimeOnly(12, 0), new TimeOnly(13, 30));
                scheduleRepo.Add(labSlot);
                added++;
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] ScheduleSlots: {added} created for {offerings.Count} offerings.");
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
        // Idempotent: skip once any Treasury fee has been seeded.
        if (await context.Set<StudentFee>().AnyAsync()) return;

        var students = await context.Students.OrderBy(s => s.StudentCode).Take(15).ToListAsync();
        // The scenarios below index up to students[12].
        if (students.Count < 13) return;

        var receiptSet = context.Set<TreasuryReceipt>();
        var feeSet = context.Set<StudentFee>();
        var orderSet = context.Set<Order>();
        var paymentSet = context.Set<Payment>();
        var txnSet = context.Set<PaymentTransaction>();

        var now = DateTime.UtcNow;
        var receiptSeq = 0;

        // Each fee is priced by a TreasuryReceipt (catalog row). In production
        // receipts are synced from the HU Treasury System; here we mint a local
        // one per fee so the required FK resolves and the receipt name carries
        // the human-readable description.
        TreasuryReceipt MakeReceipt(string name, decimal unitAmount, DateTime createdAt)
        {
            var receipt = new TreasuryReceipt
            {
                ExternalReceiptId = $"SEED-RCPT-{++receiptSeq:D4}",
                ConnectionTypeId = 6,
                Name = name,
                UnitAmount = unitAmount,
                Currency = "EGP",
                IsActive = true,
                CreatedAt = createdAt,
            };
            receiptSet.Add(receipt);
            return receipt;
        }

        // An outstanding (unpaid) obligation — the only thing the student fees
        // dashboard surfaces (its read path filters on FeeStatus.Pending).
        void MakePendingFee(Guid studentId, string name, decimal amount, string sourceModule, DateTime createdAt)
        {
            var receipt = MakeReceipt(name, amount, createdAt);
            feeSet.Add(new StudentFee
            {
                StudentId = studentId,
                ReceiptId = receipt.Id,
                Quantity = 1,
                UnitAmount = amount,
                TotalAmount = amount,
                Currency = "EGP",
                Status = FeeStatus.Pending,
                SourceModule = sourceModule,
                CreatedAt = createdAt,
            });
        }

        // A settled fee (Paid or Refunded) plus the Order, immutable Payment, and
        // gateway audit transaction that settlement would have produced.
        void MakeSettledFee(
            Guid studentId, string name, decimal amount, string sourceModule,
            DateTime createdAt, DateTime settledAt, Gateway gateway, string merchantOrderId,
            string idempotencyKey, FeeStatus feeStatus, OrderStatus orderStatus, TransactionType txnType)
        {
            var receipt = MakeReceipt(name, amount, createdAt);
            var order = new Order
            {
                StudentId = studentId,
                Status = orderStatus,
                Gateway = gateway,
                MerchantOrderId = merchantOrderId,
                TotalAmount = amount,
                Currency = "EGP",
                CreatedAt = createdAt,
            };
            var fee = new StudentFee
            {
                StudentId = studentId,
                ReceiptId = receipt.Id,
                Quantity = 1,
                UnitAmount = amount,
                TotalAmount = amount,
                Currency = "EGP",
                Status = feeStatus,
                SourceModule = sourceModule,
                OrderId = order.Id,
                CreatedAt = createdAt,
            };
            orderSet.Add(order);
            feeSet.Add(fee);
            // Payment.FeeId is UNIQUE — exactly one settlement record per fee.
            paymentSet.Add(new Payment
            {
                FeeId = fee.Id,
                OrderId = order.Id,
                Amount = amount,
                Gateway = gateway,
                MerchantOrderId = merchantOrderId,
                PaidAt = settledAt,
                CreatedAt = settledAt,
            });
            txnSet.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                MerchantOrderId = merchantOrderId,
                Gateway = gateway,
                Type = txnType,
                Status = GatewayTransactionStatus.Succeeded,
                Amount = amount,
                GatewayReference = merchantOrderId,
                IdempotencyKey = idempotencyKey,
                CreatedAt = settledAt,
            });
        }

        // ── students[1] — tuition paid (bank transfer) + outstanding activity fee ──
        MakeSettledFee(students[1].Id, "مصاريف الترم الأول - 2025 - الفرقة الرابعة", 37_500.00m,
            "registration",
            new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 9, 25, 10, 30, 0, DateTimeKind.Utc),
            Gateway.BankMisr, "BNK-20250925-002", "idem-sarainv1",
            FeeStatus.Paid, OrderStatus.Paid, TransactionType.Webhook);
        MakePendingFee(students[1].Id, "الأنشطة الطلابية - 2025", 500.00m, "admin", now.AddDays(-30));

        // ── students[6] — civil-engineering tuition paid (card) ──
        MakeSettledFee(students[6].Id, "مصاريف الترم الأول - 2025 - الهندسة المدنية", 18_750.00m,
            "registration",
            new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 10, 1, 12, 0, 0, DateTimeKind.Utc),
            Gateway.Mastercard, "TXN-20251001-003", "idem-omarinv1",
            FeeStatus.Paid, OrderStatus.Paid, TransactionType.Webhook);

        // ── students[8] — tuition refunded + outstanding course-equivalency fee ──
        MakeSettledFee(students[8].Id, "مصاريف الترم الأول - 2025 (ملغية)", 37_500.00m,
            "registration",
            new DateTime(2025, 8, 15, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 9, 15, 14, 0, 0, DateTimeKind.Utc),
            Gateway.Mastercard, "TXN-20250915-004", "idem-aliinv1",
            FeeStatus.Refunded, OrderStatus.Refunded, TransactionType.Refund);
        MakePendingFee(students[8].Id, "رسوم معادلة مواد", 3_400.00m, "services", now.AddDays(-10));

        // ── students[9] — tuition still outstanding ──
        // The legacy "partially paid" state has no Treasury equivalent (a fee is
        // atomic — paid in full or not), so it maps to a single Pending obligation.
        MakePendingFee(students[9].Id, "مصاريف الترم الأول - 2025", 37_500.00m, "registration",
            new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc));

        // ── students[10] — transcript fee paid (e-finance) ──
        MakeSettledFee(students[10].Id, "رسوم طلب بيان درجات", 150.00m, "student_services",
            now.AddDays(-10), now.AddDays(-5),
            Gateway.EFinance, "FAW-20260601-006", "idem-karim-transcript",
            FeeStatus.Paid, OrderStatus.Paid, TransactionType.Webhook);

        // ── students[11] — graduation fee outstanding ──
        MakePendingFee(students[11].Id, "رسوم التخرج واصدار الشهادة", 500.00m, "student_services",
            now.AddDays(-30));

        // ── students[12] — ID-card fee outstanding after a failed payment attempt ──
        // The fee stays Pending (released back from the failed order); the failed
        // attempt survives as an Order + gateway audit row with no Payment.
        var hassanCreated = now.AddDays(-3);
        MakePendingFee(students[12].Id, "رسوم إصدار كارنية طالب", 85.00m, "student_services", hassanCreated);
        var failedOrder = new Order
        {
            StudentId = students[12].Id,
            Status = OrderStatus.Failed,
            Gateway = Gateway.Mastercard,
            MerchantOrderId = "TXN-20260604-007",
            TotalAmount = 85.00m,
            Currency = "EGP",
            CreatedAt = hassanCreated,
        };
        orderSet.Add(failedOrder);
        txnSet.Add(new PaymentTransaction
        {
            OrderId = failedOrder.Id,
            MerchantOrderId = "TXN-20260604-007",
            Gateway = Gateway.Mastercard,
            Type = TransactionType.Webhook,
            Status = GatewayTransactionStatus.Failed,
            Amount = 85.00m,
            GatewayReference = "TXN-20260604-007",
            RawResponse = "{\"error\":\"insufficient_funds\"}",
            IdempotencyKey = "idem-hassan-idcard-fail",
            CreatedAt = hassanCreated,
        });

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] Payments: seeded Treasury fees/orders/payments for multiple students.");
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
