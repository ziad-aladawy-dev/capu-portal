using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Persistence.Seeders;

public static class StudentServicesSeeder
{
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

        logger?.LogInformation("Seeding StudentServices data with realistic examples...");

        var universityNode = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.University);
        var engineeringFaculty = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.Faculty && n.Name.Contains("الهندسة"));
        var csProgram = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.Program && n.Name.Contains("علوم الحاسب"));
        var csLevel1Node = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Name.Contains("\"ar\":\"المستوى الأول\"") && n.Parent!.Name.Contains("علوم الحاسب"));
        var csLevel4Node = await coreDbContext.StructureNodes
            .FirstOrDefaultAsync(n => n.Name.Contains("\"ar\":\"المستوى الرابع\"") && n.Parent!.Name.Contains("علوم الحاسب"));

        // -----------------------------------------------------------------
        // 1. خدمة عامة (Form + Review + Payment) مع حقل ملف داخل النموذج
        // -----------------------------------------------------------------
        var generalWorkflow = new Workflow
        {
            Name = "Workflow for General Service",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Order = 1,
                    Title = "تعبئة البيانات",
                    Description = "أدخل بياناتك الشخصية",
                    StepType = WorkflowStepType.Form,
                    IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new WorkflowStepField { Order = 1, Label = "الاسم الكامل", FieldType = StepFieldType.Text, IsRequired = true },
                        new WorkflowStepField { Order = 2, Label = "البريد الإلكتروني", FieldType = StepFieldType.Text, IsRequired = true },
                        new WorkflowStepField { Order = 3, Label = "المرفقات", FieldType = StepFieldType.File, IsRequired = false }
                    }
                },
                new WorkflowStep
                {
                    Order = 2,
                    Title = "مراجعة",
                    Description = "راجع بياناتك",
                    StepType = WorkflowStepType.Review,
                    IsRequired = true
                },
                new WorkflowStep
                {
                    Order = 3,
                    Title = "الدفع",
                    Description = "إتمام الدفع (اختياري مجاني)",
                    StepType = WorkflowStepType.Payment,
                    IsRequired = false
                }
            }
        };
        dbContext.Workflows.Add(generalWorkflow);
        await dbContext.SaveChangesAsync();

        var generalService = new Service
        {
            Name = "خدمة عامة (متاحة للجميع)",
            Type = ServiceType.General,
            Description = "هذه خدمة عامة متاحة لجميع الطلاب بغض النظر عن كليتهم أو مستواهم.",
            IsActive = true,
            IsPaid = false,
            Price = 0,
            IncludeDescendants = false,
            LevelOrder = null,
            WorkflowId = generalWorkflow.Id,
            Workflow = generalWorkflow
        };
        dbContext.Services.Add(generalService);
        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // 2. خدمة على مستوى الجامعة
        // -----------------------------------------------------------------
        if (universityNode != null)
        {
            var uniWorkflow = new Workflow
            {
                Name = "Workflow for University Service",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Order = 1,
                        Title = "طلب عام",
                        Description = "طلب على مستوى الجامعة",
                        StepType = WorkflowStepType.Form,
                        IsRequired = true,
                        Fields = new List<WorkflowStepField>
                        {
                            new WorkflowStepField { Order = 1, Label = "تفاصيل الطلب", FieldType = StepFieldType.TextArea, IsRequired = true }
                        }
                    },
                    new WorkflowStep
                    {
                        Order = 2,
                        Title = "مراجعة",
                        Description = "مراجعة الطلب",
                        StepType = WorkflowStepType.Review,
                        IsRequired = true
                    }
                }
            };
            dbContext.Workflows.Add(uniWorkflow);
            await dbContext.SaveChangesAsync();

            var uniService = new Service
            {
                Name = "خدمة الجامعة (لجميع الطلاب)",
                Type = ServiceType.Administrative,
                Description = "خدمة تقدم للطلاب من أي كلية أو مستوى، لأنها مرتبطة بالجامعة مع IncludeDescendants = true.",
                IsActive = true,
                IsPaid = false,
                Price = 0,
                IncludeDescendants = true,
                LevelOrder = null,
                WorkflowId = uniWorkflow.Id,
                Workflow = uniWorkflow
            };
            dbContext.Services.Add(uniService);
            await dbContext.SaveChangesAsync();

            var uniScope = new ServiceStructureNode
            {
                ServiceId = uniService.Id,
                StructureNodeId = universityNode.Id
            };
            dbContext.ServiceStructureNodes.Add(uniScope);
        }

        // -----------------------------------------------------------------
        // 3. خدمة كلية الهندسة (مدفوعة)
        // -----------------------------------------------------------------
        if (engineeringFaculty != null)
        {
            var facultyWorkflow = new Workflow
            {
                Name = "Workflow for Engineering Faculty Service",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Order = 1,
                        Title = "طلب كلية الهندسة",
                        Description = "خدمة خاصة بكلية الهندسة",
                        StepType = WorkflowStepType.Form,
                        IsRequired = true,
                        Fields = new List<WorkflowStepField>
                        {
                            new WorkflowStepField { Order = 1, Label = "رقم الطالب", FieldType = StepFieldType.Text, IsRequired = true }
                        }
                    },
                    new WorkflowStep
                    {
                        Order = 2,
                        Title = "الدفع",
                        Description = "دفع رسوم الخدمة",
                        StepType = WorkflowStepType.Payment,
                        IsRequired = true
                    }
                }
            };
            dbContext.Workflows.Add(facultyWorkflow);
            await dbContext.SaveChangesAsync();

            var facultyService = new Service
            {
                Name = "خدمة كلية الهندسة",
                Type = ServiceType.Specialized,
                Description = "خدمة متاحة لجميع طلاب كلية الهندسة (جميع الأقسام والبرامج والمستويات) لأن IncludeDescendants = true.",
                IsActive = true,
                IsPaid = true,
                Price = 100,
                IncludeDescendants = true,
                LevelOrder = null,
                WorkflowId = facultyWorkflow.Id,
                Workflow = facultyWorkflow
            };
            dbContext.Services.Add(facultyService);
            await dbContext.SaveChangesAsync();

            var facultyScope = new ServiceStructureNode
            {
                ServiceId = facultyService.Id,
                StructureNodeId = engineeringFaculty.Id
            };
            dbContext.ServiceStructureNodes.Add(facultyScope);
        }

        // -----------------------------------------------------------------
        // 4. خدمة برنامج علوم الحاسب (مجانية)
        // -----------------------------------------------------------------
        if (csProgram != null)
        {
            var programWorkflow = new Workflow
            {
                Name = "Workflow for CS Program Service",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Order = 1,
                        Title = "طلب برنامج علوم الحاسب",
                        Description = "خدمة خاصة ببرنامج علوم الحاسب فقط (بدون وراثة)",
                        StepType = WorkflowStepType.Form,
                        IsRequired = true,
                        Fields = new List<WorkflowStepField>
                        {
                            new WorkflowStepField { Order = 1, Label = "الكود الدراسي", FieldType = StepFieldType.Text, IsRequired = true }
                        }
                    }
                }
            };
            dbContext.Workflows.Add(programWorkflow);
            await dbContext.SaveChangesAsync();

            var programService = new Service
            {
                Name = "خدمة برنامج علوم الحاسب (لطلاب البرنامج فقط)",
                Type = ServiceType.Specialized,
                Description = "هذه الخدمة متاحة فقط للطلاب المسجلين في برنامج علوم الحاسب (بغض النظر عن مستواهم). لأن IncludeDescendants = false، لكن المستويات أبناء البرنامج لذا ستظهر لهم.",
                IsActive = true,
                IsPaid = false,
                Price = 0,
                IncludeDescendants = false,
                LevelOrder = null,
                WorkflowId = programWorkflow.Id,
                Workflow = programWorkflow
            };
            dbContext.Services.Add(programService);
            await dbContext.SaveChangesAsync();

            var programScope = new ServiceStructureNode
            {
                ServiceId = programService.Id,
                StructureNodeId = csProgram.Id
            };
            dbContext.ServiceStructureNodes.Add(programScope);
        }

        // -----------------------------------------------------------------
        // 5. خدمة التخرج - المستوى الرابع (مدفوعة)
        // -----------------------------------------------------------------
        if (csLevel4Node != null)
        {
            var levelWorkflow = new Workflow
            {
                Name = "Workflow for Level 4 CS Service",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Order = 1,
                        Title = "طلب التخرج",
                        Description = "خدمة خاصة بطلاب المستوى الرابع علوم حاسب فقط",
                        StepType = WorkflowStepType.Form,
                        IsRequired = true,
                        Fields = new List<WorkflowStepField>
                        {
                            new WorkflowStepField { Order = 1, Label = "مشروع التخرج", FieldType = StepFieldType.TextArea, IsRequired = true }
                        }
                    },
                    new WorkflowStep
                    {
                        Order = 2,
                        Title = "الدفع",
                        Description = "دفع رسوم خدمة التخرج",
                        StepType = WorkflowStepType.Payment,
                        IsRequired = true
                    }
                }
            };
            dbContext.Workflows.Add(levelWorkflow);
            await dbContext.SaveChangesAsync();

            var levelService = new Service
            {
                Name = "خدمة التخرج - المستوى الرابع علوم حاسب",
                Type = ServiceType.Administrative,
                Description = "خدمة متاحة فقط لطلاب المستوى الرابع في برنامج علوم الحاسب.",
                IsActive = true,
                IsPaid = true,
                Price = 500,
                IncludeDescendants = false,
                LevelOrder = 4,
                WorkflowId = levelWorkflow.Id,
                Workflow = levelWorkflow
            };
            dbContext.Services.Add(levelService);
            await dbContext.SaveChangesAsync();

            var levelScope = new ServiceStructureNode
            {
                ServiceId = levelService.Id,
                StructureNodeId = csLevel4Node.Id
            };
            dbContext.ServiceStructureNodes.Add(levelScope);
        }

        // -----------------------------------------------------------------
        // 6. خدمة الإرشاد الأكاديمي - المستوى الأول
        // -----------------------------------------------------------------
        if (csLevel1Node != null)
        {
            var firstLevelWorkflow = new Workflow
            {
                Name = "Workflow for First Level CS Service",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Order = 1,
                        Title = "توجيه وإرشاد",
                        Description = "خدمة لطلاب المستوى الأول",
                        StepType = WorkflowStepType.Form,
                        IsRequired = true,
                        Fields = new List<WorkflowStepField>()
                    }
                }
            };
            dbContext.Workflows.Add(firstLevelWorkflow);
            await dbContext.SaveChangesAsync();

            var firstLevelService = new Service
            {
                Name = "خدمة الإرشاد الأكاديمي - المستوى الأول",
                Type = ServiceType.General,
                Description = "خدمة متاحة فقط لطلاب المستوى الأول في برنامج علوم الحاسب (باستخدام LevelOrder=1)",
                IsActive = true,
                IsPaid = false,
                Price = 0,
                IncludeDescendants = false,
                LevelOrder = 1,
                WorkflowId = firstLevelWorkflow.Id,
                Workflow = firstLevelWorkflow
            };
            dbContext.Services.Add(firstLevelService);
            await dbContext.SaveChangesAsync();

            var firstLevelScope = new ServiceStructureNode
            {
                ServiceId = firstLevelService.Id,
                StructureNodeId = csProgram.Id
            };
            dbContext.ServiceStructureNodes.Add(firstLevelScope);
        }

        await dbContext.SaveChangesAsync();
        logger?.LogInformation("StudentServices seeding completed.");
    }
}