using CapitalUniversity.Core.Domain.UniversityStructure.Enums;
using CapitalUniversity.Core.Infrastructure.Persistence;
using CapitalUniversity.Module.StudentServices.Abstractions.PublicApi;
using CapitalUniversity.Module.StudentServices.Domain;
using Microsoft.Data.SqlClient;
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

        logger?.LogInformation("Seeding StudentServices data...");

        var firstStudent = await coreDbContext.Students
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        if (firstStudent == null)
            logger?.LogWarning("No students found. Skipping request seeding.");

        var workflow = new Workflow
        {
            Name = "Standard Service Workflow",
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Order = 1,
                    Title = "Personal Information",
                    Description = "Please fill your personal details",
                    StepType = WorkflowStepType.Form,
                    IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new WorkflowStepField { Order = 1, Label = "Full Name", FieldType = StepFieldType.Text, IsRequired = true },
                        new WorkflowStepField { Order = 2, Label = "National ID", FieldType = StepFieldType.Text, IsRequired = true }
                    }
                },
                new WorkflowStep
                {
                    Order = 2,
                    Title = "Upload Documents",
                    Description = "Upload required documents",
                    StepType = WorkflowStepType.FileUpload,
                    IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new WorkflowStepField { Order = 1, Label = "ID Copy", FieldType = StepFieldType.File, IsRequired = true }
                    }
                },
                new WorkflowStep
                {
                    Order = 3,
                    Title = "Review",
                    Description = "Review your information",
                    StepType = WorkflowStepType.Review,
                    IsRequired = true
                },
                new WorkflowStep
                {
                    Order = 4,
                    Title = "Payment",
                    Description = "Complete payment",
                    StepType = WorkflowStepType.Payment,
                    IsRequired = true
                },
                new WorkflowStep
                {
                    Order = 5,
                    Title = "Submit",
                    Description = "Final submission",
                    StepType = WorkflowStepType.Submit,
                    IsRequired = true
                }
            }
        };
        dbContext.Workflows.Add(workflow);

        var service = new Service
        {
            Name = "Example Student Service",
            Type = ServiceType.General,
            Description = "This is an example service for demonstration.",
            IsActive = true,
            IsPaid = true,
            Price = 50,
            IncludeDescendants = true,
            AcademicYearId = null,
            Workflow = workflow
        };

        var universityNode = await coreDbContext.StructureNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.University);

        if (universityNode != null)
        {
            logger?.LogInformation($"Found university node with Id: {universityNode.Id}");
            service.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = universityNode.Id });
        }
        else
        {
            logger?.LogWarning("No university node found. Service will be global (no scope restrictions).");
        }

        dbContext.Services.Add(service);

        try
        {
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Service and workflow saved successfully.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
        {
            logger?.LogWarning("FK violation when adding scope node. Removing scope nodes and retrying.");
            service.ScopeNodes.Clear();
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Service saved without scope restrictions (global).");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error while saving service.");
            throw;
        }

        if (firstStudent != null)
        {
            var request = new StudentRequest
            {
                StudentId = firstStudent.Id,
                ServiceId = service.Id,
                Status = RequestStatus.Draft,
                PaymentStatus = PaymentStatus.Pending,
                SubmittedData = "{}",
                CurrentStepOrder = 0,
                HistoryEntries = new List<RequestHistoryEntry>
                {
                    new RequestHistoryEntry
                    {
                        Action = "Created",
                        PerformedByUserId = firstStudent.Id,
                        PerformedByRole = "Student",
                        PerformedAt = DateTime.UtcNow
                    }
                }
            };
            dbContext.StudentRequests.Add(request);
            await dbContext.SaveChangesAsync();
            logger?.LogInformation($"Created example request for student {firstStudent.Id}");
        }

        logger?.LogInformation("StudentServices seeding completed.");
    }
}