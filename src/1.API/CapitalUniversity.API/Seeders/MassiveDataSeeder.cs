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
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Domain.Notifications;
using CapitalUniversity.Modules.Registration.Domain;
using CapitalUniversity.Modules.Registration.Abstractions;
using CapitalUniversity.Modules.AcademicRecords.Domain;
using CapitalUniversity.Modules.AcademicRecords.Abstractions;
using CapitalUniversity.Module.StudentServices.Domain;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CapitalUniversity.API.Seeders;

public static class MassiveDataSeeder
{
    public static async Task SeedAsync(CoreDbContext context, IPasswordHasher passwordHasher, IServiceProvider serviceProvider)
    {
        // Courses and AcademicPlans are now seeded by DataSeeder in Core.Infrastructure.
        await ExpandStudentsAsync(context, passwordHasher);
        await EnsureHistoricalYearsAsync(context);
        await SeedCourseOfferingsAsync(context);
        await SeedScheduleSlotsAsync(context);
        await ExpandStaffAsync(context, passwordHasher);
        await AssignInstructorsAsync(context);
        await SeedPastOfferingsAsync(context);
        await SeedAcademicHistoryAsync(context);
        await ExpandPaymentsAsync(context);
        await ExpandTreasuryHistoryAsync(context);
        await SeedStudentProfileRecordsAsync(context);
        await SeedPortalProfileRecordsAsync(context);
        await SeedStudentNotificationsAsync(context);
        await SeedPortalServicesAsync(serviceProvider);
        await SeedStudentRequestsAsync(serviceProvider);
        await BackfillSyncIdentityAsync(context);
    }

    // ════════════════════════════════════════════════════════════════
    //  SYNC IDENTITY BACKFILL
    //  The Sync Platform addresses rows ONLY by ExternallySourced.ExternalId:
    //  Student/Staff pulls are update-only (a row without an ExternalId can
    //  never be matched and the batch is skipped), and Course/Offering/Slot
    //  pulls would INSERT a duplicate that dead-letters on the unique Code /
    //  section index. Stamping deterministic SIS-style ids on seeded rows
    //  makes every one of them reachable by the sync pipelines (e.g. POST
    //  /admin/outbox/student/SIS-STU-20250001 round-trips onto the seeded
    //  student). Rows created later through real sync keep their upstream ids.
    // ════════════════════════════════════════════════════════════════

    private static async Task BackfillSyncIdentityAsync(CoreDbContext context)
    {
        var stamped = 0;

        foreach (var s in await context.Students
                     .Where(s => s.ExternallySourced.ExternalId == null).ToListAsync())
        {
            s.ExternallySourced.ExternalId = $"SIS-STU-{s.StudentCode}";
            stamped++;
        }

        foreach (var s in await context.Staffs
                     .Where(s => s.ExternallySourced.ExternalId == null).ToListAsync())
        {
            s.ExternallySourced.ExternalId = $"SIS-EMP-{s.EmployeeCode}";
            stamped++;
        }

        foreach (var c in await context.Courses
                     .Where(c => c.ExternallySourced.ExternalId == null).ToListAsync())
        {
            c.ExternallySourced.ExternalId = $"SIS-CRS-{c.Code}";
            stamped++;
        }

        foreach (var o in await context.Set<CourseOffering>()
                     .Where(o => o.ExternallySourced.ExternalId == null).ToListAsync())
        {
            o.ExternallySourced.ExternalId = $"SIS-OFF-{o.Id:N}";
            stamped++;
        }

        foreach (var s in await context.Set<ScheduleSlot>()
                     .Where(s => s.ExternallySourced.ExternalId == null).ToListAsync())
        {
            s.ExternallySourced.ExternalId = $"SIS-SLT-{s.Id:N}";
            stamped++;
        }

        if (stamped > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"[MassSeed] SyncIdentity: stamped ExternalId on {stamped} row(s).");
        }
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

    // ════════════════════════════════════════════════════════════════
    //  HISTORICAL ACADEMIC YEARS (2021-2022, 2022-2023)
    //  Older cohorts (level 4/5 students) started before the years the
    //  base DataSeeder creates; their registration history needs terms
    //  to attach to.
    // ════════════════════════════════════════════════════════════════

    private static async Task EnsureHistoricalYearsAsync(CoreDbContext context)
    {
        var existingNames = await context.AcademicYears.Select(y => y.Name).ToHashSetAsync();

        var fallName = LocalizedJson.Of("خريف", "Fall");
        var springName = LocalizedJson.Of("ربيع", "Spring");
        var summerName = LocalizedJson.Of("صيف", "Summer");

        var defs = new[]
        {
            ("2021-2022", new DateTime(2021, 9, 1), new DateTime(2022, 8, 31)),
            ("2022-2023", new DateTime(2022, 9, 1), new DateTime(2023, 8, 31)),
        };

        var added = 0;
        foreach (var (name, start, end) in defs)
        {
            if (existingNames.Contains(name)) continue;
            var ay = new AcademicYear { Id = Guid.NewGuid(), Name = name, StartDate = start, EndDate = end, IsCurrent = false };
            ay.Semesters = new List<Semester>
            {
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = fallName,   Order = 1, StartDate = start,               EndDate = start.AddMonths(4), IsCurrent = false },
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = springName, Order = 2, StartDate = start.AddMonths(5),  EndDate = start.AddMonths(9), IsCurrent = false },
                new() { Id = Guid.NewGuid(), AcademicYearId = ay.Id, Name = summerName, Order = 3, StartDate = start.AddMonths(10), EndDate = end,                IsCurrent = false },
            };
            context.AcademicYears.Add(ay);
            added++;
        }

        if (added > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"[MassSeed] AcademicYears: +{added} historical year(s).");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  INSTRUCTOR STAFF
    // ════════════════════════════════════════════════════════════════

    private static async Task ExpandStaffAsync(CoreDbContext context, IPasswordHasher passwordHasher)
    {
        var nodes = await context.StructureNodes.ToListAsync();
        StructureNode? FindNode(string name) => nodes.FirstOrDefault(n => n.Name.Contains(name));

        var pwd = passwordHasher.HashPassword("admin123");
        var jobTitles = new[]
        {
            LocalizedJson.Of("أستاذ", "Professor"),
            LocalizedJson.Of("أستاذ مساعد", "Associate Professor"),
            LocalizedJson.Of("مدرس", "Lecturer"),
        };

        var defs = new (string Emp, string Name, string NodeName)[]
        {
            ("INS-001", "Dr. Hossam Farid",       "Computer & Systems Engineering"),
            ("INS-002", "Dr. Mona El-Naggar",     "Computer & Systems Engineering"),
            ("INS-003", "Dr. Sherif Abdel-Hamid", "Civil Engineering"),
            ("INS-004", "Dr. Heba Lotfy",         "Architectural Engineering"),
            ("INS-005", "Dr. Walid Mansour",      "Mechanical Engineering"),
            ("INS-006", "Dr. Rania Fouad",        "Electrical Engineering"),
            ("INS-007", "Dr. Ayman Roshdy",       "Biomedical Engineering"),
            ("INS-008", "Dr. Ghada Sami",         "Communications & Information Engineering"),
            ("INS-009", "Dr. Nahla Selim",        "Clinical Nutrition"),
            ("INS-010", "Dr. Sameh Ghoneim",      "Nutrition & Food Science"),
            ("INS-011", "Dr. Abeer Zaki",         "Textile & Clothing"),
            ("INS-012", "Dr. Magdy Hafez",        "General Stream"),
        };

        var existingCodes = await context.Staffs.Select(s => s.EmployeeCode).ToHashSetAsync();
        var existingNids = await context.Staffs.Select(s => s.NationalId).ToHashSetAsync();
        var added = 0;
        var newStaff = new List<(Staff Staff, StructureNode Node)>();

        for (var i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            if (existingCodes.Contains(d.Emp)) continue;
            var node = FindNode(d.NodeName);
            if (node == null) continue;
            var nid = $"27{60 + i:D2}0101{2346 + i:D4}67";
            if (existingNids.Contains(nid)) continue;

            var staff = new Staff
            {
                Id = Guid.NewGuid(),
                EmployeeCode = d.Emp,
                Name = d.Name,
                NationalId = nid,
                BirthDate = new DateTime(1972 + (i % 12), 1 + (i % 11), 5 + i, 0, 0, 0, DateTimeKind.Utc),
                PhoneNumber = $"011122{2200 + i:D4}",
                Email = $"ins{i + 1:D3}@capital.edu.eg",
                Role = "Staff",
                JobTitle = jobTitles[i % jobTitles.Length],
                StructureNodeId = node.Id,
                PasswordHash = pwd,
                PasswordExpiry = DateTime.UtcNow.AddYears(5),
                IsActive = true,
            };
            context.Staffs.Add(staff);
            newStaff.Add((staff, node));
            added++;
        }

        if (added == 0) return;
        await context.SaveChangesAsync();

        // Scoped "Staff" role assignment so instructors authenticate with a
        // working permission set, tenanted to their program node.
        var staffRole = (await context.Roles.ToListAsync())
            .FirstOrDefault(r => LocalizedJson.Extract(r.Name, "en") == "Staff");
        if (staffRole != null)
        {
            var existingAssignments = await context.StaffRoles.ToListAsync();
            foreach (var (staff, node) in newStaff)
            {
                if (existingAssignments.Any(a => a.StaffId == staff.Id && a.RoleId == staffRole.Id)) continue;
                context.StaffRoles.Add(new StaffRoleAssignment(staff.Id, staffRole.Id, ScopeKeys.Global, ScopeKeys.Global)
                {
                    StructureNodeId = node.Id,
                    StructureNodePath = node.Path,
                });
            }
            await context.SaveChangesAsync();
        }

        Console.WriteLine($"[MassSeed] Staff: +{added} instructors.");
    }

    private static async Task AssignInstructorsAsync(CoreDbContext context)
    {
        var unassigned = await context.Set<CourseOffering>()
            .Where(o => o.InstructorId == null)
            .OrderBy(o => o.Id)
            .ToListAsync();
        if (unassigned.Count == 0) return;

        var instructors = await context.Staffs
            .Where(s => s.IsActive && s.EmployeeCode.StartsWith("INS-"))
            .OrderBy(s => s.EmployeeCode)
            .Select(s => s.Id)
            .ToListAsync();
        if (instructors.Count == 0)
        {
            instructors = await context.Staffs.Where(s => s.IsActive).Select(s => s.Id).ToListAsync();
        }
        if (instructors.Count == 0) return;

        for (var i = 0; i < unassigned.Count; i++)
        {
            unassigned[i].InstructorId = instructors[i % instructors.Count];
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] Offerings: {unassigned.Count} instructor assignment(s).");
    }

    // ════════════════════════════════════════════════════════════════
    //  PAST-SEMESTER COURSE OFFERINGS (admin matrix history)
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedPastOfferingsAsync(CoreDbContext context)
    {
        var years = await context.AcademicYears.Include(y => y.Semesters).ToListAsync();
        var pastYears = years.Where(y => y.Name is "2023-2024" or "2024-2025").ToList();
        if (pastYears.Count == 0) return;

        var pastSemIds = pastYears.SelectMany(y => y.Semesters)
            .Where(s => s.Order <= 2)
            .Select(s => s.Id)
            .ToHashSet();
        if (await context.Set<CourseOffering>().AnyAsync(o => pastSemIds.Contains(o.SemesterId))) return;

        var plans = await context.AcademicPlans.Include(p => p.PlanCourses).ToListAsync();
        var courseIds = await context.Courses.Select(c => c.Id).ToHashSetAsync();
        var offeringSet = context.Set<CourseOffering>();
        var added = 0;

        foreach (var year in pastYears)
        {
            var fall = year.Semesters.FirstOrDefault(s => s.Order == 1);
            var spring = year.Semesters.FirstOrDefault(s => s.Order == 2);

            foreach (var plan in plans)
            {
                foreach (var pc in plan.PlanCourses)
                {
                    if (!courseIds.Contains(pc.CourseId)) continue;
                    var target = pc.Semester == 1 ? fall : pc.Semester == 2 ? spring : null;
                    if (target == null) continue;

                    var offering = new CourseOffering
                    {
                        Id = Guid.NewGuid(),
                        CourseId = pc.CourseId,
                        SemesterId = target.Id,
                        StructureNodeId = plan.StructureNodeId,
                        SectionCode = "A",
                        CreatedAt = DateTime.SpecifyKind(year.StartDate, DateTimeKind.Utc),
                    };
                    offering.InitializeCapacity(60);
                    offering.Activate();
                    // Registration stays Closed: these terms are over.
                    offeringSet.Add(offering);
                    added++;
                }
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] PastOfferings: {added} created for {pastYears.Count} past year(s).");
    }

    // ════════════════════════════════════════════════════════════════
    //  PER-STUDENT ACADEMIC HISTORY
    //  Plan-driven registrations + grades across every year the student
    //  has been enrolled, plus a GPA summary snapshot. Drives: My Courses,
    //  Grades, Transcript, registration history, admin academic hub.
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedAcademicHistoryAsync(CoreDbContext context)
    {
        if (await context.Set<StudentRegisteredCourse>().AnyAsync()) return;

        var now = DateTime.UtcNow;
        var students = await context.Students.OrderBy(s => s.StudentCode).ToListAsync();
        var nodes = await context.StructureNodes.ToDictionaryAsync(n => n.Id);
        var courses = await context.Courses.ToDictionaryAsync(c => c.Id);
        var plans = await context.AcademicPlans.Include(p => p.PlanCourses)
            .Where(p => p.IsActive).ToListAsync();
        var plansByNode = plans.GroupBy(p => p.StructureNodeId)
            .ToDictionary(g => g.Key, g => g.First());
        var years = await context.AcademicYears.Include(y => y.Semesters).ToListAsync();
        var yearsByName = years.ToDictionary(y => y.Name);
        var yearsById = years.ToDictionary(y => y.Id);

        var currentYear = years.FirstOrDefault(y => y.IsCurrent)
            ?? years.Where(y => y.StartDate <= now).OrderBy(y => y.StartDate).LastOrDefault();
        if (currentYear == null) return;
        var currentSemester = years.SelectMany(y => y.Semesters).FirstOrDefault(s => s.IsCurrent)
            ?? currentYear.Semesters.OrderBy(s => s.Order).First();
        var currentStartYear = int.Parse(currentYear.Name.Split('-')[0]);

        (DateTime YearStart, int Order) SemKey(Semester s) => (yearsById[s.AcademicYearId].StartDate, s.Order);
        var currentKey = SemKey(currentSemester);
        int CompareToCurrent(Semester s)
        {
            var k = SemKey(s);
            var byYear = k.YearStart.CompareTo(currentKey.YearStart);
            return byYear != 0 ? byYear : k.Order.CompareTo(currentKey.Order);
        }

        // Section-A offerings of the current semester, keyed by (course, program
        // node), so Enrolled rows bump the live RegisteredCount counters.
        var currentOfferings = (await context.Set<CourseOffering>()
                .Where(o => o.SemesterId == currentSemester.Id).ToListAsync())
            .Where(o => o.SectionCode == "A")
            .GroupBy(o => (o.CourseId, o.StructureNodeId))
            .ToDictionary(g => g.Key, g => g.First());

        var regSet = context.Set<StudentRegisteredCourse>();
        var resSet = context.Set<StudentAcademicResult>();
        var snapSet = context.Set<AcademicSummarySnapshot>();

        var rng = new Random(20260611);
        var gradeBands = new (string Letter, decimal Points, int Score, int Weight)[]
        {
            ("A", 4.0m, 95, 10), ("A-", 3.7m, 90, 10), ("B+", 3.3m, 87, 14), ("B", 3.0m, 83, 16),
            ("B-", 2.7m, 80, 12), ("C+", 2.3m, 76, 12), ("C", 2.0m, 72, 10), ("C-", 1.7m, 68, 6),
            ("D+", 1.3m, 65, 5), ("D", 1.0m, 61, 5),
        };
        (string Letter, decimal Points, decimal Score) PickGrade(int fromBand, int toBand)
        {
            var slice = gradeBands[fromBand..(toBand + 1)];
            var total = slice.Sum(b => b.Weight);
            var roll = rng.Next(total);
            foreach (var b in slice)
            {
                roll -= b.Weight;
                if (roll < 0) return (b.Letter, b.Points, b.Score + rng.Next(-2, 3));
            }
            var last = slice[^1];
            return (last.Letter, last.Points, last.Score);
        }

        var regCount = 0;
        var resCount = 0;
        var snapCount = 0;

        foreach (var student in students)
        {
            if (!nodes.TryGetValue(student.StructureNodeId, out var levelNode)) continue;
            if (levelNode.Type != StructureNodeType.Level || levelNode.ParentId == null) continue;
            var programId = levelNode.ParentId.Value;
            if (!plansByNode.TryGetValue(programId, out var plan) || plan.PlanCourses.Count == 0) continue;

            var siblingLevels = nodes.Values
                .Where(n => n.ParentId == programId && n.Type == StructureNodeType.Level)
                .OrderBy(n => n.Order).ThenBy(n => n.Id)
                .ToList();
            var levelIdx = siblingLevels.FindIndex(n => n.Id == levelNode.Id) + 1;
            if (levelIdx <= 0) continue;
            levelIdx = Math.Min(levelIdx, plan.PlanCourses.Max(pc => pc.Level));

            var attempts = new HashSet<Guid>();
            // Latest-attempt graded rows feeding GPA: (semester key, points, credits, passed)
            var graded = new List<((DateTime YearStart, int Order) Key, decimal Points, int Credits, bool Passed)>();

            StudentRegisteredCourse AddRegistration(Course course, Semester sem, int attempt,
                RegistrationStatus status, DateTime registeredAt, DateTime? completedAt)
            {
                var reg = new StudentRegisteredCourse
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    CourseId = course.Id,
                    SemesterId = sem.Id,
                    StructureNodeId = levelNode.Id,
                    AttemptNumber = attempt,
                    RegistrationStatus = status,
                    RegisteredAt = registeredAt,
                    CompletedAt = completedAt,
                    CreatedAt = registeredAt,
                    ExternallySourced = new ExternallySourcedData
                    {
                        ExternalId = $"SEED-REG-{student.StudentCode}-{course.Code}-{attempt}",
                        LastSyncedAt = now,
                    },
                };
                regSet.Add(reg);
                regCount++;
                return reg;
            }

            void AddResult(StudentRegisteredCourse reg, Course course, int attempt, AcademicResultStatus status,
                string? grade, decimal? score, int creditsEarned, bool isLatest)
            {
                resSet.Add(new StudentAcademicResult
                {
                    Id = Guid.NewGuid(),
                    StudentRegisteredCourseId = reg.Id,
                    Grade = grade,
                    NumericScore = score,
                    Status = status,
                    CreditsEarned = creditsEarned,
                    IsLatestAttempt = isLatest,
                    ExternallySourced = new ExternallySourcedData
                    {
                        ExternalId = $"SEED-RES-{student.StudentCode}-{course.Code}-{attempt}",
                        LastSyncedAt = now,
                    },
                });
                resCount++;
            }

            void EnrollCurrent(Course course, Semester sem, int attempt)
            {
                var registeredAt = DateTime.SpecifyKind(sem.StartDate, DateTimeKind.Utc).AddDays(-rng.Next(2, 12));
                var reg = AddRegistration(course, sem, attempt, RegistrationStatus.Enrolled, registeredAt, null);
                AddResult(reg, course, attempt, AcademicResultStatus.InProgress, null, null, 0, true);
                if (currentOfferings.TryGetValue((course.Id, programId), out var offering)
                    && offering.RegisteredCount < offering.Capacity
                    && offering.Status != OfferingStatus.Cancelled)
                {
                    offering.IncrementRegistration();
                }
            }

            for (var lvl = 1; lvl <= levelIdx; lvl++)
            {
                var startYear = currentStartYear - (levelIdx - lvl);
                if (!yearsByName.TryGetValue($"{startYear}-{startYear + 1}", out var year)) continue;

                foreach (var pc in plan.PlanCourses.Where(p => p.Level == lvl)
                             .OrderBy(p => p.Semester).ThenBy(p => p.CourseId))
                {
                    if (!courses.TryGetValue(pc.CourseId, out var course)) continue;
                    if (!attempts.Add(course.Id)) continue; // already taken at an earlier level
                    var sem = year.Semesters.FirstOrDefault(s => s.Order == pc.Semester);
                    if (sem == null) continue;

                    var cmp = CompareToCurrent(sem);
                    if (cmp > 0) continue; // future term

                    if (cmp == 0)
                    {
                        EnrollCurrent(course, sem, 1);
                        continue;
                    }

                    // Completed term.
                    var semStartUtc = DateTime.SpecifyKind(sem.StartDate, DateTimeKind.Utc);
                    var semEndUtc = DateTime.SpecifyKind(sem.EndDate, DateTimeKind.Utc);
                    var registeredAt = semStartUtc.AddDays(-rng.Next(2, 12));
                    var roll = rng.Next(100);

                    if (roll < 86)
                    {
                        var (letter, points, score) = PickGrade(0, gradeBands.Length - 1);
                        var reg = AddRegistration(course, sem, 1, RegistrationStatus.Completed, registeredAt, semEndUtc);
                        AddResult(reg, course, 1, AcademicResultStatus.Passed, letter, score, course.CreditHours, true);
                        graded.Add((SemKey(sem), points, course.CreditHours, true));
                        continue;
                    }

                    // Failed (8%) or withdrawn (6%) — then retake in the same
                    // term of the following academic year when it has arrived.
                    var failed = roll < 94;
                    StudentRegisteredCourse firstReg;
                    if (failed)
                    {
                        firstReg = AddRegistration(course, sem, 1, RegistrationStatus.Failed, registeredAt, semEndUtc);
                    }
                    else
                    {
                        firstReg = AddRegistration(course, sem, 1, RegistrationStatus.Withdrawn, registeredAt,
                            semStartUtc.AddDays(45));
                    }

                    Semester? retakeSem = null;
                    if (yearsByName.TryGetValue($"{startYear + 1}-{startYear + 2}", out var retakeYear))
                    {
                        var candidate = retakeYear.Semesters.FirstOrDefault(s => s.Order == pc.Semester);
                        if (candidate != null && CompareToCurrent(candidate) <= 0) retakeSem = candidate;
                    }

                    if (retakeSem == null)
                    {
                        // No retake happened yet — first attempt is the latest.
                        if (failed)
                        {
                            AddResult(firstReg, course, 1, AcademicResultStatus.Failed, "F", 40 + rng.Next(0, 18), 0, true);
                            graded.Add((SemKey(sem), 0m, course.CreditHours, false));
                        }
                        else
                        {
                            AddResult(firstReg, course, 1, AcademicResultStatus.Withdrawn, null, null, 0, true);
                        }
                        continue;
                    }

                    if (failed)
                    {
                        AddResult(firstReg, course, 1, AcademicResultStatus.Failed, "F", 40 + rng.Next(0, 18), 0, false);
                    }
                    else
                    {
                        AddResult(firstReg, course, 1, AcademicResultStatus.Withdrawn, null, null, 0, false);
                    }

                    if (CompareToCurrent(retakeSem) == 0)
                    {
                        EnrollCurrent(course, retakeSem, 2);
                    }
                    else
                    {
                        var retakeStartUtc = DateTime.SpecifyKind(retakeSem.StartDate, DateTimeKind.Utc);
                        var retakeEndUtc = DateTime.SpecifyKind(retakeSem.EndDate, DateTimeKind.Utc);
                        var retakeReg = AddRegistration(course, retakeSem, 2, RegistrationStatus.Completed,
                            retakeStartUtc.AddDays(-rng.Next(2, 12)), retakeEndUtc);
                        var (letter, points, score) = PickGrade(2, 7); // retakes land C-..B+ territory
                        AddResult(retakeReg, course, 2, AcademicResultStatus.Passed, letter, score, course.CreditHours, true);
                        graded.Add((SemKey(retakeSem), points, course.CreditHours, true));
                    }
                }
            }

            // ── GPA summary snapshot (computed from the latest-attempt rows above) ──
            var totalPlanCredits = plan.PlanCourses
                .Where(pc => courses.ContainsKey(pc.CourseId))
                .Sum(pc => courses[pc.CourseId].CreditHours);
            var earned = graded.Where(g => g.Passed).Sum(g => g.Credits);
            var failedHours = graded.Where(g => !g.Passed).Sum(g => g.Credits);
            var gradedCredits = graded.Sum(g => g.Credits);
            var cgpa = gradedCredits > 0
                ? Math.Round(graded.Sum(g => g.Points * g.Credits) / gradedCredits, 2)
                : 0m;
            var lastKey = graded.Count > 0 ? graded.Max(g => g.Key) : default;
            var lastTerm = graded.Where(g => g.Key == lastKey).ToList();
            var lastTermCredits = lastTerm.Sum(g => g.Credits);
            var gpa = lastTermCredits > 0
                ? Math.Round(lastTerm.Sum(g => g.Points * g.Credits) / lastTermCredits, 2)
                : 0m;

            snapSet.Add(new AcademicSummarySnapshot
            {
                Id = Guid.NewGuid(),
                StudentId = student.Id,
                Gpa = gpa,
                Cgpa = cgpa,
                EarnedCredits = earned,
                RemainingCredits = Math.Max(0, totalPlanCredits - earned),
                PassedHours = earned,
                FailedHours = failedHours,
                AcademicStanding = gradedCredits == 0 || cgpa >= 2.0m ? "Good Standing" : "Probation",
                ExternallySourced = new ExternallySourcedData
                {
                    ExternalId = $"SEED-SUM-{student.StudentCode}",
                    LastSyncedAt = now,
                },
            });
            snapCount++;
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] AcademicHistory: {regCount} registrations, {resCount} results, {snapCount} summaries.");
    }

    // ════════════════════════════════════════════════════════════════
    //  MULTI-YEAR TREASURY HISTORY
    //  Tuition + recurring fees per student per enrolled year, settled
    //  for past years and mixed paid/pending for the current one.
    // ════════════════════════════════════════════════════════════════

    private static async Task ExpandTreasuryHistoryAsync(CoreDbContext context)
    {
        const string marker = "SEED-HIST-";
        var receiptSet = context.Set<TreasuryReceipt>();
        if (await receiptSet.AnyAsync(r => r.ExternalReceiptId.StartsWith(marker))) return;

        var now = DateTime.UtcNow;
        var students = await context.Students.OrderBy(s => s.StudentCode).ToListAsync();
        var nodes = await context.StructureNodes.ToDictionaryAsync(n => n.Id);
        var plans = await context.AcademicPlans.Where(p => p.IsActive).Include(p => p.PlanCourses).ToListAsync();
        var planLevelsByNode = plans.GroupBy(p => p.StructureNodeId)
            .ToDictionary(g => g.Key, g => g.First().PlanCourses.Count > 0 ? g.First().PlanCourses.Max(pc => pc.Level) : 0);
        var years = await context.AcademicYears.ToListAsync();
        var yearsByName = years.ToDictionary(y => y.Name);
        var currentYear = years.FirstOrDefault(y => y.IsCurrent)
            ?? years.Where(y => y.StartDate <= now).OrderBy(y => y.StartDate).LastOrDefault();
        if (currentYear == null) return;
        var currentStartYear = int.Parse(currentYear.Name.Split('-')[0]);

        var feeSet = context.Set<StudentFee>();
        var orderSet = context.Set<Order>();
        var paymentSet = context.Set<Payment>();
        var txnSet = context.Set<PaymentTransaction>();

        // Students already covered by the hand-written scenarios above keep
        // their current-year rows; only their history is back-filled.
        var alreadyBilled = await feeSet.Select(f => f.StudentId).Distinct().ToHashSetAsync();

        var receiptCache = new Dictionary<string, TreasuryReceipt>();
        var receiptSeq = 0;
        TreasuryReceipt GetReceipt(string name, decimal amount, DateTime createdAt)
        {
            var key = $"{name}|{amount}";
            if (receiptCache.TryGetValue(key, out var cached)) return cached;
            var receipt = new TreasuryReceipt
            {
                ExternalReceiptId = $"{marker}{++receiptSeq:D4}",
                ConnectionTypeId = 6,
                Name = name,
                UnitAmount = amount,
                Currency = "EGP",
                IsActive = true,
                CreatedAt = createdAt,
            };
            receiptSet.Add(receipt);
            receiptCache[key] = receipt;
            return receipt;
        }

        var rng = new Random(20260612);
        var gateways = new[] { Gateway.Mastercard, Gateway.BankMisr, Gateway.EFinance };
        var orderSeq = 0;
        var feeCount = 0;

        void AddPending(Guid studentId, string name, decimal amount, string sourceModule, DateTime createdAt)
        {
            var receipt = GetReceipt(name, amount, createdAt);
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
            feeCount++;
        }

        void AddSettled(Student student, string name, decimal amount, string sourceModule,
            DateTime createdAt, DateTime paidAt)
        {
            var receipt = GetReceipt(name, amount, createdAt);
            var gateway = gateways[orderSeq % gateways.Length];
            var merchantOrderId = $"SEED-{student.StudentCode}-{++orderSeq:D5}";
            var order = new Order
            {
                StudentId = student.Id,
                Status = OrderStatus.Paid,
                Gateway = gateway,
                MerchantOrderId = merchantOrderId,
                TotalAmount = amount,
                Currency = "EGP",
                CreatedAt = createdAt,
            };
            var fee = new StudentFee
            {
                StudentId = student.Id,
                ReceiptId = receipt.Id,
                Quantity = 1,
                UnitAmount = amount,
                TotalAmount = amount,
                Currency = "EGP",
                Status = FeeStatus.Paid,
                SourceModule = sourceModule,
                OrderId = order.Id,
                CreatedAt = createdAt,
            };
            orderSet.Add(order);
            feeSet.Add(fee);
            paymentSet.Add(new Payment
            {
                FeeId = fee.Id,
                OrderId = order.Id,
                Amount = amount,
                Gateway = gateway,
                MerchantOrderId = merchantOrderId,
                PaidAt = paidAt,
                CreatedAt = paidAt,
            });
            txnSet.Add(new PaymentTransaction
            {
                OrderId = order.Id,
                MerchantOrderId = merchantOrderId,
                Gateway = gateway,
                Type = TransactionType.Webhook,
                Status = GatewayTransactionStatus.Succeeded,
                Amount = amount,
                GatewayReference = merchantOrderId,
                IdempotencyKey = $"idem-{merchantOrderId}",
                CreatedAt = paidAt,
            });
            feeCount++;
        }

        foreach (var student in students)
        {
            if (!nodes.TryGetValue(student.StructureNodeId, out var levelNode)) continue;
            if (levelNode.Type != StructureNodeType.Level || levelNode.ParentId == null) continue;
            var programId = levelNode.ParentId.Value;

            var siblingLevels = nodes.Values
                .Where(n => n.ParentId == programId && n.Type == StructureNodeType.Level)
                .OrderBy(n => n.Order).ThenBy(n => n.Id)
                .ToList();
            var levelIdx = siblingLevels.FindIndex(n => n.Id == levelNode.Id) + 1;
            if (levelIdx <= 0) continue;
            if (planLevelsByNode.TryGetValue(programId, out var maxLvl) && maxLvl > 0)
            {
                levelIdx = Math.Min(levelIdx, maxLvl);
            }

            // Faculty = level → program → system → faculty.
            var isEngineering = false;
            var cursor = levelNode;
            while (cursor.ParentId != null && nodes.TryGetValue(cursor.ParentId.Value, out var parent))
            {
                if (parent.Type == StructureNodeType.Faculty)
                {
                    isEngineering = parent.Name.Contains("Engineering", StringComparison.OrdinalIgnoreCase);
                    break;
                }
                cursor = parent;
            }
            var tuition = isEngineering ? 37_500.00m : 22_500.00m;

            for (var lvl = 1; lvl <= levelIdx; lvl++)
            {
                var startYear = currentStartYear - (levelIdx - lvl);
                var yearName = $"{startYear}-{startYear + 1}";
                if (!yearsByName.ContainsKey(yearName)) continue;
                var isCurrentYearRow = startYear == currentStartYear;
                var createdAt = new DateTime(startYear, 9, 1, 8, 0, 0, DateTimeKind.Utc);
                var paidAt = createdAt.AddDays(rng.Next(10, 35));

                // Hand-written scenario students keep their current-year rows.
                if (isCurrentYearRow && alreadyBilled.Contains(student.Id)) continue;

                if (!isCurrentYearRow || rng.Next(100) < 60)
                {
                    AddSettled(student, $"مصاريف العام الدراسي {yearName}", tuition, "registration", createdAt, paidAt);
                }
                else
                {
                    AddPending(student.Id, $"مصاريف العام الدراسي {yearName}", tuition, "registration", createdAt);
                }

                if (!isCurrentYearRow || rng.Next(100) < 80)
                {
                    AddSettled(student, $"رسوم الأنشطة الطلابية {yearName}", 500.00m, "admin", createdAt, paidAt.AddDays(2));
                }
                else
                {
                    AddPending(student.Id, $"رسوم الأنشطة الطلابية {yearName}", 500.00m, "admin", createdAt);
                }

                if (isEngineering)
                {
                    AddSettled(student, $"رسوم المعامل والورش {yearName}", 1_200.00m, "admin", createdAt, paidAt.AddDays(4));
                }
            }

            // Sprinkled current-year extras.
            if (rng.Next(100) < 12)
            {
                AddPending(student.Id, "غرامة تأخير كتب المكتبة", 50.00m, "library", now.AddDays(-rng.Next(5, 40)));
            }
            if (rng.Next(100) < 15)
            {
                AddPending(student.Id, "رسوم تجديد كارنيه الطالب", 85.00m, "student_services", now.AddDays(-rng.Next(3, 25)));
            }
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] TreasuryHistory: {feeCount} fees across years for {students.Count} students.");
    }

    // ════════════════════════════════════════════════════════════════
    //  PORTAL-MANAGED PROFILE RECORDS
    //  The student portal's blocker gate requires a phone number, an
    //  address (Custom record keyed "contact-information") and an
    //  emergency contact whose DataJson uses "name"/"phone" keys.
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedPortalProfileRecordsAsync(CoreDbContext context)
    {
        var recordSet = context.Set<StudentProfileRecord>();
        var students = await context.Students.OrderBy(s => s.StudentCode).ToListAsync();
        var existing = await recordSet
            .Where(r => r.Category == StudentProfileCategory.Custom || r.Category == StudentProfileCategory.EmergencyContact)
            .ToListAsync();

        var cities = new[] { "Cairo", "Giza", "Helwan", "Shubra El-Kheima", "Maadi", "Nasr City", "6th of October", "New Cairo" };
        var added = 0;
        var repaired = 0;

        foreach (var student in students)
        {
            var hash = (student.StudentCode.GetHashCode() & int.MaxValue);

            // Contact extras (address/city/country) — read by the portal
            // profile page and the completeness gate.
            var hasContact = existing.Any(r => r.StudentId == student.Id
                && r.Category == StudentProfileCategory.Custom
                && r.CustomCategoryKey == "contact-information");
            if (!hasContact)
            {
                recordSet.Add(new StudentProfileRecord
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    Category = StudentProfileCategory.Custom,
                    CustomCategoryKey = "contact-information",
                    SchemaVersion = 1,
                    DataJson = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["address"] = $"{10 + hash % 80} El-Nasr Street, Apt {1 + hash % 12}",
                        ["city"] = cities[hash % cities.Length],
                        ["country"] = "Egypt",
                    }),
                });
                added++;
            }

            // Emergency contact — the gate reads "name"/"phone".
            var emergency = existing.FirstOrDefault(r => r.StudentId == student.Id
                && r.Category == StudentProfileCategory.EmergencyContact);
            if (emergency == null)
            {
                recordSet.Add(new StudentProfileRecord
                {
                    Id = Guid.NewGuid(),
                    StudentId = student.Id,
                    Category = StudentProfileCategory.EmergencyContact,
                    SchemaVersion = 1,
                    DataJson = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["name"] = $"Guardian of {student.Name.Split(' ')[0]}",
                        ["relationship"] = hash % 2 == 0 ? "Father" : "Mother",
                        ["phone"] = $"0122{1000000 + hash % 9000000:D7}",
                    }),
                });
                added++;
            }
            else if (!emergency.DataJson.Contains("\"name\""))
            {
                // Older seed wrote "contact_name"; remap so the gate passes.
                try
                {
                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(emergency.DataJson)
                        ?? new Dictionary<string, JsonElement>();
                    var output = data.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
                    if (output.TryGetValue("contact_name", out var legacyName)) output["name"] = legacyName;
                    if (!output.ContainsKey("phone")) output["phone"] = $"0122{1000000 + hash % 9000000:D7}";
                    emergency.DataJson = JsonSerializer.Serialize(output);
                    repaired++;
                }
                catch
                {
                    // Leave malformed JSON untouched.
                }
            }
        }

        if (added > 0 || repaired > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"[MassSeed] PortalProfileRecords: +{added} added, {repaired} repaired.");
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  STUDENT (and extra staff) NOTIFICATIONS
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStudentNotificationsAsync(CoreDbContext context)
    {
        var students = await context.Students.OrderBy(s => s.StudentCode).ToListAsync();
        if (students.Count == 0) return;
        var studentIds = students.Select(s => s.Id).ToList();
        if (await context.Notifications.AnyAsync(n => studentIds.Contains(n.RecipientUserId))) return;

        var now = DateTime.UtcNow;
        var years = await context.AcademicYears.ToListAsync();
        var currentYear = years.FirstOrDefault(y => y.IsCurrent)
            ?? years.Where(y => y.StartDate <= now).OrderBy(y => y.StartDate).LastOrDefault();
        var currentYearStart = currentYear != null
            ? DateTime.SpecifyKind(currentYear.StartDate, DateTimeKind.Utc)
            : new DateTime(now.Year, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        var rng = new Random(20260613);
        var added = 0;

        void Notify(Guid recipient, string ar, string en, string arMsg, string enMsg,
            NotificationType type, bool isRead, DateTime createdAt)
        {
            context.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                RecipientUserId = recipient,
                Title = LocalizedJson.Of(ar, en),
                Message = LocalizedJson.Of(arMsg, enMsg),
                Type = type,
                IsRead = isRead,
                CreatedAt = createdAt,
            });
            added++;
        }

        foreach (var student in students)
        {
            var firstName = student.Name.Split(' ')[0];

            Notify(student.Id,
                "مرحباً بك في بوابة جامعة العاصمة", "Welcome to Capital University Portal",
                $"أهلاً {firstName}! تم تفعيل حسابك على البوابة الإلكترونية. يمكنك الآن متابعة مقرراتك ودرجاتك ومدفوعاتك.",
                $"Hello {firstName}! Your portal account is now active. You can track your courses, grades, and payments.",
                NotificationType.Info, true, currentYearStart.AddDays(-rng.Next(30, 700)));

            Notify(student.Id,
                "تأكيد تسجيل المقررات", "Course Registration Confirmed",
                "تم تسجيل مقررات الفصل الدراسي الحالي بنجاح. راجع جدولك الدراسي من صفحة الجدول.",
                "Your registration for the current term was completed successfully. Check your weekly schedule page.",
                NotificationType.Info, true, currentYearStart.AddDays(rng.Next(2, 8)));

            Notify(student.Id,
                "تم نشر جدول المحاضرات", "Lecture Schedule Published",
                "تم نشر الجدول الدراسي للفصل الحالي. قد تطرأ تعديلات على القاعات خلال الأسبوع الأول.",
                "The timetable for the current term has been published. Room changes may occur during the first week.",
                NotificationType.Info, rng.Next(100) < 50, currentYearStart.AddDays(rng.Next(8, 15)));

            Notify(student.Id,
                "إعلان نتائج الفصل الدراسي", "Semester Grades Published",
                "تم اعتماد ونشر نتائج الفصل الدراسي الماضي. يمكنك الاطلاع عليها من صفحة الدرجات.",
                "Last term's grades have been approved and published. View them on your grades page.",
                NotificationType.Info, false, now.AddDays(-rng.Next(3, 20)));

            Notify(student.Id,
                "إيصال سداد", "Payment Receipt",
                "تم استلام سداد المصروفات الدراسية بنجاح. شكراً لالتزامك بمواعيد السداد.",
                "Your tuition payment was received successfully. Thank you for paying on time.",
                NotificationType.Info, true, currentYearStart.AddDays(rng.Next(15, 45)));

            if (rng.Next(100) < 40)
            {
                Notify(student.Id,
                    "تنبيه: مستحقات مالية", "Reminder: Outstanding Fees",
                    "توجد مستحقات مالية غير مسددة على حسابك. برجاء السداد قبل نهاية الشهر لتجنب غرامات التأخير.",
                    "You have unpaid fees on your account. Please settle them before month end to avoid late penalties.",
                    NotificationType.Warning, false, now.AddDays(-rng.Next(2, 10)));
            }

            if (rng.Next(100) < 30)
            {
                Notify(student.Id,
                    "تذكير من المكتبة", "Library Reminder",
                    "لديك كتب مستعارة يقترب موعد إرجاعها. برجاء التجديد أو الإرجاع خلال أسبوع.",
                    "You have borrowed books due soon. Please renew or return them within a week.",
                    NotificationType.Info, true, now.AddDays(-rng.Next(10, 30)));
            }
        }

        // A few fresh items for every staff inbox too.
        var staff = await context.Staffs.Where(s => s.IsActive).ToListAsync();
        foreach (var member in staff)
        {
            Notify(member.Id,
                "طلبات طلابية بانتظار المراجعة", "Student Requests Awaiting Review",
                "توجد طلبات خدمات طلابية جديدة بانتظار الإسناد والمراجعة في لوحة الطلبات.",
                "New student service requests are awaiting assignment and review on the requests board.",
                NotificationType.Info, false, now.AddDays(-rng.Next(1, 5)));
            Notify(member.Id,
                "تحديث الجداول الدراسية", "Timetable Update",
                "تم تحديث جداول الفصل الدراسي الحالي. برجاء مراجعة التعارضات إن وجدت.",
                "The current term timetables were updated. Please review any conflicts.",
                NotificationType.Info, rng.Next(100) < 50, now.AddDays(-rng.Next(5, 15)));
        }

        await context.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] Notifications: +{added} (students + staff).");
    }

    // ════════════════════════════════════════════════════════════════
    //  PORTAL STUDENT SERVICES
    //  The base StudentServicesSeeder scopes its services to the demo
    //  "Computer Science" tree where no seeded student lives. These are
    //  scoped to the real student tree (root + faculties) so the portal
    //  catalog is populated for everyone.
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedPortalServicesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentServicesDbContext>();
        var core = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        if (await db.Services.AnyAsync(s => s.Name.Contains("بيان درجات"))) return;

        // Resolve the tree the seeded students actually live in: walk up from a
        // student's level node to its root rather than guessing by name.
        var anyStudent = await core.Students.FirstOrDefaultAsync();
        if (anyStudent == null) return;
        var nodes = await core.StructureNodes.ToDictionaryAsync(n => n.Id);
        if (!nodes.TryGetValue(anyStudent.StructureNodeId, out var probe)) return;
        while (probe.ParentId != null && nodes.TryGetValue(probe.ParentId.Value, out var parentNode)) probe = parentNode;
        var root = probe;

        // Faculties under that root that actually have students.
        var studentNodeIds = await core.Students.Select(s => s.StructureNodeId).Distinct().ToListAsync();
        var faculties = new Dictionary<Guid, StructureNode>();
        foreach (var nodeId in studentNodeIds)
        {
            if (!nodes.TryGetValue(nodeId, out var cursor)) continue;
            while (cursor != null)
            {
                if (cursor.Type == StructureNodeType.Faculty)
                {
                    faculties[cursor.Id] = cursor;
                    break;
                }
                cursor = cursor.ParentId != null && nodes.TryGetValue(cursor.ParentId.Value, out var p) ? p : null;
            }
        }

        Workflow MakeWorkflow(string name, bool paid, params (string Label, StepFieldType Type, bool Required, string? Options)[] fields)
        {
            var steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1,
                    Title = "بيانات الطلب",
                    Description = "استكمل بيانات الطلب المطلوبة",
                    StepType = WorkflowStepType.Form,
                    IsRequired = true,
                    Fields = fields.Select((f, i) => new WorkflowStepField
                    {
                        Order = i + 1,
                        Label = f.Label,
                        FieldType = f.Type,
                        IsRequired = f.Required,
                        OptionsJson = f.Options,
                    }).ToList(),
                },
            };
            var next = 2;
            if (paid)
            {
                steps.Add(new WorkflowStep
                {
                    Order = next++,
                    Title = "سداد الرسوم",
                    Description = "سداد رسوم الخدمة إلكترونياً",
                    StepType = WorkflowStepType.Payment,
                    IsRequired = true,
                });
            }
            steps.Add(new WorkflowStep
            {
                Order = next,
                Title = "مراجعة واعتماد",
                Description = "مراجعة الطلب من الموظف المختص",
                StepType = WorkflowStepType.Review,
                IsRequired = true,
            });
            return new Workflow { Name = name, Steps = steps };
        }

        async Task AddServiceAsync(string name, string description, ServiceType type, bool paid, decimal price,
            Workflow workflow, params StructureNode[] scopeNodes)
        {
            db.Workflows.Add(workflow);
            await db.SaveChangesAsync();

            var service = new Service
            {
                Name = name,
                Description = description,
                Type = type,
                IsActive = true,
                IsPaid = paid,
                Price = paid ? price : null,
                IncludeDescendants = true,
                WorkflowId = workflow.Id,
            };
            db.Services.Add(service);
            await db.SaveChangesAsync();

            foreach (var node in scopeNodes)
            {
                db.ServiceStructureNodes.Add(new ServiceStructureNode
                {
                    ServiceId = service.Id,
                    StructureNodeId = node.Id,
                });
            }
            await db.SaveChangesAsync();
        }

        var copiesOptions = JsonSerializer.Serialize(new[] { "نسخة واحدة", "نسختان", "ثلاث نسخ" });
        var languageOptions = JsonSerializer.Serialize(new[] { "العربية", "الإنجليزية", "العربية والإنجليزية" });
        var reasonOptions = JsonSerializer.Serialize(new[] { "ظرف صحي", "ظرف عائلي", "سفر", "أخرى" });

        await AddServiceAsync(
            "طلب بيان درجات رسمي",
            "إصدار بيان درجات رسمي معتمد بجميع المقررات والتقديرات الحاصل عليها الطالب.",
            ServiceType.Administrative, true, 150m,
            MakeWorkflow("Workflow: Transcript Request", true,
                ("عدد النسخ", StepFieldType.Select, true, copiesOptions),
                ("اللغة", StepFieldType.Select, true, languageOptions),
                ("الغرض من الطلب", StepFieldType.TextArea, false, null)),
            root);

        await AddServiceAsync(
            "شهادة قيد",
            "إصدار شهادة قيد رسمية تفيد انتظام الطالب بالدراسة في العام الجامعي الحالي.",
            ServiceType.Administrative, true, 75m,
            MakeWorkflow("Workflow: Enrollment Certificate", true,
                ("الجهة الموجهة إليها الشهادة", StepFieldType.Text, true, null),
                ("اللغة", StepFieldType.Select, true, languageOptions)),
            root);

        await AddServiceAsync(
            "بدل فاقد كارنيه الطالب",
            "إصدار كارنيه بديل في حالة الفقد أو التلف.",
            ServiceType.General, true, 85m,
            MakeWorkflow("Workflow: ID Card Replacement", true,
                ("سبب الاستخراج", StepFieldType.Select, true, JsonSerializer.Serialize(new[] { "فقد", "تلف", "تغيير بيانات" })),
                ("ملاحظات", StepFieldType.TextArea, false, null)),
            root);

        await AddServiceAsync(
            "طلب إيقاف قيد / تأجيل",
            "تقديم طلب إيقاف قيد أو تأجيل الدراسة لفصل دراسي أو عام كامل وفقاً للائحة.",
            ServiceType.Administrative, false, 0m,
            MakeWorkflow("Workflow: Leave of Absence", false,
                ("نوع الإيقاف", StepFieldType.Select, true, JsonSerializer.Serialize(new[] { "فصل دراسي", "عام كامل" })),
                ("السبب", StepFieldType.Select, true, reasonOptions),
                ("تفاصيل إضافية", StepFieldType.TextArea, true, null)),
            root);

        await AddServiceAsync(
            "تظلم من نتيجة مقرر",
            "تقديم تظلم رسمي لإعادة رصد أو مراجعة درجة مقرر دراسي.",
            ServiceType.Specialized, false, 0m,
            MakeWorkflow("Workflow: Grade Appeal", false,
                ("كود المقرر", StepFieldType.Text, true, null),
                ("الفصل الدراسي", StepFieldType.Text, true, null),
                ("أسباب التظلم", StepFieldType.TextArea, true, null)),
            root);

        await AddServiceAsync(
            "طلب انسحاب من مقرر",
            "الانسحاب من مقرر دراسي خلال الفترة المسموح بها دون احتساب رسوب.",
            ServiceType.General, false, 0m,
            MakeWorkflow("Workflow: Course Withdrawal", false,
                ("كود المقرر", StepFieldType.Text, true, null),
                ("سبب الانسحاب", StepFieldType.TextArea, true, null)),
            root);

        foreach (var faculty in faculties.Values)
        {
            var isEngineering = faculty.Name.Contains("Engineering", StringComparison.OrdinalIgnoreCase);
            if (isEngineering)
            {
                await AddServiceAsync(
                    $"خطاب تدريب ميداني - {faculty.Name}",
                    "إصدار خطاب رسمي موجه لجهة التدريب الميداني الصيفي.",
                    ServiceType.Specialized, true, 200m,
                    MakeWorkflow($"Workflow: Internship Letter ({faculty.Name})", true,
                        ("اسم جهة التدريب", StepFieldType.Text, true, null),
                        ("مدة التدريب", StepFieldType.Select, true, JsonSerializer.Serialize(new[] { "شهر", "شهران", "ثلاثة أشهر" })),
                        ("تاريخ بدء التدريب", StepFieldType.Date, true, null)),
                    faculty);
            }
            else
            {
                await AddServiceAsync(
                    $"حجز معامل - {faculty.Name}",
                    "حجز معامل الكلية لإجراء التجارب والمشروعات البحثية خارج مواعيد المحاضرات.",
                    ServiceType.Specialized, false, 0m,
                    MakeWorkflow($"Workflow: Lab Booking ({faculty.Name})", false,
                        ("المعمل المطلوب", StepFieldType.Text, true, null),
                        ("التاريخ المطلوب", StepFieldType.Date, true, null),
                        ("الغرض", StepFieldType.TextArea, true, null)),
                    faculty);
            }
        }

        Console.WriteLine($"[MassSeed] PortalServices: 6 university-wide + {faculties.Count} faculty-scoped services.");
    }

    // ════════════════════════════════════════════════════════════════
    //  STUDENT SERVICE REQUESTS
    //  Spread across every RequestStatus so the admin kanban, dashboards
    //  and the student "My Requests" history are all populated.
    // ════════════════════════════════════════════════════════════════

    private static async Task SeedStudentRequestsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudentServicesDbContext>();
        var core = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

        // A handful of hand-made requests may already exist from manual
        // testing; only skip once real seeded volume is present.
        if (await db.StudentRequests.CountAsync() >= 10) return;

        var services = await db.Services
            .Include(s => s.Workflow)!.ThenInclude(w => w!.Steps)!.ThenInclude(st => st.Fields)
            .Where(s => s.IsActive)
            .ToListAsync();
        services = services.Where(s => s.Workflow != null && s.Workflow.Steps.Count > 0).ToList();
        if (services.Count == 0) return;

        var students = await core.Students.OrderBy(s => s.StudentCode).ToListAsync();
        var staffIds = await core.Staffs.Where(s => s.IsActive).OrderBy(s => s.EmployeeCode)
            .Select(s => s.Id).ToListAsync();
        if (students.Count == 0 || staffIds.Count == 0) return;

        var now = DateTime.UtcNow;
        var rng = new Random(20260614);
        var paySeq = 0;
        var added = 0;

        string SampleValue(WorkflowStepField field, Student student)
        {
            switch (field.FieldType)
            {
                case StepFieldType.Select:
                case StepFieldType.Radio:
                    try
                    {
                        var options = JsonSerializer.Deserialize<List<string>>(field.OptionsJson ?? "[]");
                        if (options is { Count: > 0 }) return options[rng.Next(options.Count)];
                    }
                    catch { /* fall through to text */ }
                    return "الخيار الأول";
                case StepFieldType.Date:
                    return now.AddDays(rng.Next(7, 60)).ToString("yyyy-MM-dd");
                case StepFieldType.Number:
                    return rng.Next(1, 4).ToString();
                case StepFieldType.TextArea:
                    return $"طلب مقدم من الطالب {student.Name} - برجاء التكرم بالموافقة مع خالص الشكر.";
                case StepFieldType.File:
                    return "attachment.pdf";
                default:
                    return field.Label.Contains("كود") ? "CS201" : student.Name;
            }
        }

        void MakeRequest(Student student, Service service, RequestStatus status, int daysAgo)
        {
            var createdAt = now.AddDays(-daysAgo).AddHours(-rng.Next(0, 9));
            var isDraft = status == RequestStatus.Draft;
            var formSteps = service.Workflow!.Steps
                .Where(s => s.StepType == WorkflowStepType.Form)
                .OrderBy(s => s.Order)
                .ToList();

            var submittedData = new Dictionary<string, Dictionary<string, string>>();
            if (!isDraft)
            {
                foreach (var step in formSteps)
                {
                    submittedData[step.Order.ToString()] = step.Fields
                        .OrderBy(f => f.Order)
                        .ToDictionary(f => f.Label, f => SampleValue(f, student));
                }
            }

            var isAdvanced = status is RequestStatus.UnderReview or RequestStatus.MoreInfoRequired
                or RequestStatus.Approved or RequestStatus.Rejected or RequestStatus.Completed
                or RequestStatus.ReadyForPickup;
            var paid = service.IsPaid && !isDraft && status != RequestStatus.PaymentPending;

            var request = new StudentRequest
            {
                StudentId = student.Id,
                ServiceId = service.Id,
                Status = status,
                PaymentStatus = !service.IsPaid
                    ? PaymentStatus.NotRequired
                    : paid ? PaymentStatus.Paid : PaymentStatus.Pending,
                AmountPaid = paid ? service.Price : null,
                PaymentTransactionId = paid ? $"SEED-PAY-{++paySeq:D5}" : null,
                SubmittedData = JsonSerializer.Serialize(submittedData),
                CurrentStepOrder = isDraft ? 0 : formSteps.Count > 0 ? formSteps[^1].Order : 0,
                SubmittedAt = isDraft ? null : createdAt.AddHours(2),
                CompletedAt = status == RequestStatus.Completed ? createdAt.AddDays(rng.Next(2, 7)) : null,
                CreatedAt = createdAt,
                HistoryEntries = new List<RequestHistoryEntry>(),
            };

            if (isAdvanced)
            {
                request.AssignedToStaffId = staffIds[rng.Next(staffIds.Count)];
                request.AssignedAt = createdAt.AddHours(6);
            }

            var t = createdAt;
            void History(string action, string? comment, Guid? by, string role)
            {
                request.HistoryEntries.Add(new RequestHistoryEntry
                {
                    Action = action,
                    Comment = comment,
                    PerformedByUserId = by,
                    PerformedByRole = role,
                    PerformedAt = t = t.AddMinutes(rng.Next(30, 240)),
                });
            }

            History("Created", null, student.Id, "Student");
            if (paid)
            {
                History("PaymentCompleted", $"Payment of {service.Price} completed via gateway", student.Id, "Student");
            }
            if (!isDraft)
            {
                History("Submitted", null, student.Id, "Student");
            }
            if (request.AssignedToStaffId != null)
            {
                History("Assigned", "تم إسناد الطلب للمراجعة", request.AssignedToStaffId, "Staff");
            }
            if (status is RequestStatus.MoreInfoRequired)
            {
                History($"StatusChanged_{status}", "برجاء رفع صورة بطاقة الرقم القومي", request.AssignedToStaffId, "Staff");
            }
            else if (status is RequestStatus.Rejected)
            {
                History($"StatusChanged_{status}", "الطلب غير مستوفٍ لشروط اللائحة", request.AssignedToStaffId, "Staff");
            }
            else if (status is RequestStatus.Cancelled)
            {
                History($"StatusChanged_{status}", "تم الإلغاء بناءً على رغبة الطالب", student.Id, "Student");
            }
            else if (isAdvanced && status != RequestStatus.UnderReview)
            {
                History($"StatusChanged_{status}", null, request.AssignedToStaffId, "Staff");
            }

            db.StudentRequests.Add(request);
            added++;
        }

        // Guarantee coverage of every status, then add random volume on top.
        var statusCycle = new[]
        {
            RequestStatus.Draft, RequestStatus.Pending, RequestStatus.UnderReview,
            RequestStatus.MoreInfoRequired, RequestStatus.Approved, RequestStatus.Rejected,
            RequestStatus.PaymentPending, RequestStatus.Completed, RequestStatus.Cancelled,
            RequestStatus.ReadyForPickup,
        };
        var weightedPool = new[]
        {
            RequestStatus.Pending, RequestStatus.Pending, RequestStatus.UnderReview,
            RequestStatus.UnderReview, RequestStatus.Completed, RequestStatus.Completed,
            RequestStatus.Completed, RequestStatus.Approved, RequestStatus.Rejected,
            RequestStatus.ReadyForPickup, RequestStatus.Draft, RequestStatus.MoreInfoRequired,
        };

        for (var i = 0; i < statusCycle.Length * 2; i++)
        {
            var status = statusCycle[i % statusCycle.Length];
            var student = students[i % students.Count];
            // PaymentPending only makes sense on a paid service.
            var pool = status == RequestStatus.PaymentPending
                ? services.Where(s => s.IsPaid).ToList()
                : services;
            if (pool.Count == 0) continue;
            MakeRequest(student, pool[rng.Next(pool.Count)], status, rng.Next(3, 90));
        }

        foreach (var student in students)
        {
            if (rng.Next(100) >= 55) continue;
            var extra = rng.Next(1, 3);
            for (var i = 0; i < extra; i++)
            {
                var status = weightedPool[rng.Next(weightedPool.Length)];
                MakeRequest(student, services[rng.Next(services.Count)], status, rng.Next(1, 120));
            }
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"[MassSeed] StudentRequests: {added} requests with history.");
    }
}
