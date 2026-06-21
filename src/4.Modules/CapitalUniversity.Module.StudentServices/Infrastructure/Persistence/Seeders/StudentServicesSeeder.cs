using CapitalUniversity.Core.Domain.UniversityStructure;
using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Seeders;

public static class StudentServicesSeeder
{
    private static string Localized(string ar, string en)
        => JsonSerializer.Serialize(new { ar, en });

    private static string TruncateLocalized(string ar, string en, int maxAr = 25, int maxEn = 25)
    {
        var arTrunc = ar.Length > maxAr ? ar[..maxAr] : ar;
        var enTrunc = en.Length > maxEn ? en[..maxEn] : en;
        return Localized(arTrunc, enTrunc);
    }

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentServicesDbContext>();
        var coreDbContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<StudentServicesDbContext>>();

        if (await dbContext.Services.AnyAsync())
        {
            logger?.LogInformation("StudentServices data already seeded. Skipping.");
            return;
        }

        logger?.LogInformation("Seeding StudentServices with fully bilingual data...");

        var university = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.University);

        var engineeringFaculty = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.Faculty && n.Name.Contains("الهندسة"));

        var homeEconomicsFaculty = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.Faculty && n.Name.Contains("الاقتصاد المنزلي"));

        var csProgram = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.Program && n.Name.Contains("علوم الحاسب"));

        var businessProgram = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.Program && n.Name.Contains("إدارة الأعمال"));

        var csLevel1 = csProgram != null
            ? await coreDbContext.StructureNodes.FirstOrDefaultAsync(n => n.Type == StructureNodeType.Level && n.Name.Contains("المستوى الأول") && n.ParentId == csProgram.Id)
            : null;

        var csLevel4 = csProgram != null
            ? await coreDbContext.StructureNodes.FirstOrDefaultAsync(n => n.Type == StructureNodeType.Level && n.Name.Contains("المستوى الرابع") && n.ParentId == csProgram.Id)
            : null;

        async Task<Service> CreateServiceWithWorkflowAsync(
            string nameAr, string nameEn,
            string descAr, string descEn,
            ServiceType type,
            bool isPaid,
            decimal? price,
            bool includeDescendants,
            int? levelOrder,
            List<(int order, string titleAr, string titleEn, string descAr, string descEn, WorkflowStepType stepType, bool isRequired, decimal? stepPrice, List<(int order, string labelAr, string labelEn, StepFieldType fieldType, bool isRequired, List<string>? options)> fields)> steps,
            List<Guid> scopeNodeIds)
        {
            var workflowNameAr = nameAr.Length > 12 ? nameAr[..12] : nameAr;
            var workflowNameEn = nameEn.Length > 12 ? nameEn[..12] : nameEn;
            var workflowNameJson = Localized($"سير {workflowNameAr}", $"WF {workflowNameEn}");
            if (workflowNameJson.Length > 200)
            {
                workflowNameAr = nameAr.Length > 8 ? nameAr[..8] : nameAr;
                workflowNameEn = nameEn.Length > 8 ? nameEn[..8] : nameEn;
                workflowNameJson = Localized($"سير {workflowNameAr}", $"WF {workflowNameEn}");
            }

            var workflow = new Workflow
            {
                Name = workflowNameJson,
                Steps = steps.Select(s => new WorkflowStep
                {
                    Order = s.order,
                    Title = TruncateLocalized(s.titleAr, s.titleEn, 30, 30),
                    Description = TruncateLocalized(s.descAr, s.descEn, 40, 40),
                    StepType = s.stepType,
                    IsRequired = s.isRequired,
                    Price = s.stepPrice,
                    Fields = s.fields.Select(f => new WorkflowStepField
                    {
                        Order = f.order,
                        Label = TruncateLocalized(f.labelAr, f.labelEn, 25, 25),
                        FieldType = f.fieldType,
                        IsRequired = f.isRequired,
                        OptionsJson = f.options != null && f.options.Any()
                            ? JsonSerializer.Serialize(f.options)
                            : null
                    }).ToList()
                }).ToList()
            };

            await dbContext.Workflows.AddAsync(workflow);
            await dbContext.SaveChangesAsync();

            var serviceName = TruncateLocalized(nameAr, nameEn, 30, 30);
            var serviceDesc = TruncateLocalized(descAr, descEn, 50, 50);

            var service = new Service
            {
                Name = serviceName,
                Description = serviceDesc,
                Type = type,
                IsActive = true,
                IsPaid = isPaid,
                Price = price,
                IncludeDescendants = includeDescendants,
                LevelOrder = levelOrder,
                WorkflowId = workflow.Id,
                Workflow = workflow
            };

            await dbContext.Services.AddAsync(service);
            await dbContext.SaveChangesAsync();

            foreach (var nodeId in scopeNodeIds)
            {
                dbContext.ServiceStructureNodes.Add(new ServiceStructureNode
                {
                    ServiceId = service.Id,
                    StructureNodeId = nodeId
                });
            }

            await dbContext.SaveChangesAsync();
            return service;
        }

        await CreateServiceWithWorkflowAsync(
            nameAr: "خدمة عامة (للجميع)",
            nameEn: "General Service (All)",
            descAr: "خدمة عامة متاحة لكافة الطلاب دون قيود",
            descEn: "General service available to all students",
            type: ServiceType.General,
            isPaid: false,
            price: 0,
            includeDescendants: false,
            levelOrder: null,
            steps: new List<(int, string, string, string, string, WorkflowStepType, bool, decimal?, List<(int, string, string, StepFieldType, bool, List<string>?)>)>
            {
                (1,
                 "بيانات شخصية", "Personal Info",
                 "أدخل بياناتك الأساسية", "Enter basic details",
                 WorkflowStepType.Form, true, null,
                 new List<(int, string, string, StepFieldType, bool, List<string>?)>
                 {
                     (1, "الاسم الكامل", "Full Name", StepFieldType.Text, true, null),
                     (2, "رقم الهوية", "ID", StepFieldType.Text, true, null),
                     (3, "البريد الإلكتروني", "Email", StepFieldType.Text, true, null),
                     (4, "الجوال", "Mobile", StepFieldType.Text, false, null),
                     (5, "المرفقات (اختياري)", "Attachments (Optional)", StepFieldType.File, false, null)
                 }),
                (2,
                 "مراجعة الطلب", "Review",
                 "راجع بياناتك قبل الإرسال", "Review before submission",
                 WorkflowStepType.Review, true, null,
                 new List<(int, string, string, StepFieldType, bool, List<string>?)>()),
                (3,
                 "الدفع (مجاني)", "Payment (Free)",
                 "الخدمة مجانية", "Service is free",
                 WorkflowStepType.Payment, false, null,
                 new List<(int, string, string, StepFieldType, bool, List<string>?)>())
            },
            scopeNodeIds: new List<Guid>()
        );

        if (university != null)
        {
            await CreateServiceWithWorkflowAsync(
                nameAr: "خدمة إدارية - الجامعة",
                nameEn: "Admin Service - University",
                descAr: "خدمة إدارية تشمل جميع الكليات والأقسام",
                descEn: "Administrative service covering all faculties",
                type: ServiceType.Administrative,
                isPaid: true,
                price: 50,
                includeDescendants: true,
                levelOrder: null,
                steps: new List<(int, string, string, string, string, WorkflowStepType, bool, decimal?, List<(int, string, string, StepFieldType, bool, List<string>?)>)>
                {
                    (1,
                     "طلب إداري", "Admin Request",
                     "تقديم طلب إداري", "Submit admin request",
                     WorkflowStepType.Form, true, null,
                     new List<(int, string, string, StepFieldType, bool, List<string>?)>
                     {
                         (1, "نوع الطلب", "Type", StepFieldType.Select, true, new List<string> { "شهادة", "تصديق", "معادلة", "استفسار" }),
                         (2, "التفاصيل", "Details", StepFieldType.TextArea, true, null),
                         (3, "المرفقات", "Attachments", StepFieldType.File, false, null)
                     }),
                    (2,
                     "مراجعة الإدارة", "Admin Review",
                     "مراجعة من قبل الإدارة", "Review by admin",
                     WorkflowStepType.Review, true, null,
                     new List<(int, string, string, StepFieldType, bool, List<string>?)>()),
                    (3,
                     "الدفع", "Payment",
                     "دفع الرسوم", "Pay fees",
                     WorkflowStepType.Payment, true, 50,
                     new List<(int, string, string, StepFieldType, bool, List<string>?)>())
                },
                scopeNodeIds: new List<Guid> { university.Id }
            );
        }

        if (engineeringFaculty != null)
        {
            await CreateServiceWithWorkflowAsync(
                nameAr: "خدمة كلية الهندسة",
                nameEn: "Engineering Faculty Service",
                descAr: "خدمة لطلاب كلية الهندسة (جميع الأقسام)",
                descEn: "Service for Engineering students (all depts)",
                type: ServiceType.Specialized,
                isPaid: true,
                price: 100,
                includeDescendants: true,
                levelOrder: null,
                steps: new List<(int, string, string, string, string, WorkflowStepType, bool, decimal?, List<(int, string, string, StepFieldType, bool, List<string>?)>)>
                {
                    (1,
                     "طلب كلية الهندسة", "Eng Faculty Request",
                     "خدمة خاصة بطلاب الهندسة", "Service for Engineering students",
                     WorkflowStepType.Form, true, null,
                     new List<(int, string, string, StepFieldType, bool, List<string>?)>
                     {
                         (1, "القسم", "Department", StepFieldType.Select, true, new List<string> { "مدني", "كهربائي", "ميكانيكي", "كيميائي", "حاسوب" }),
                         (2, "الرقم الجامعي", "University ID", StepFieldType.Text, true, null),
                         (3, "الموضوع", "Subject", StepFieldType.Text, true, null),
                         (4, "الوصف", "Description", StepFieldType.TextArea, false, null)
                     }),
                    (2,
                     "مراجعة الكلية", "Faculty Review",
                     "مراجعة من قبل الكلية", "Review by faculty",
                     WorkflowStepType.Review, true, null,
                     new List<(int, string, string, StepFieldType, bool, List<string>?)>()),
                    (3,
                     "الدفع", "Payment",
                     "دفع رسوم الخدمة", "Pay service fee",
                     WorkflowStepType.Payment, true, 100,
                     new List<(int, string, string, StepFieldType, bool, List<string>?)>())
                },
                scopeNodeIds: new List<Guid> { engineeringFaculty.Id }
            );
        }

        await dbContext.SaveChangesAsync();
        logger?.LogInformation("StudentServices seeding completed successfully.");
    }
}