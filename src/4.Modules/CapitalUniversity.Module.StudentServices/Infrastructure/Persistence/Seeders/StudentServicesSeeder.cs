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
        var context = scope.ServiceProvider.GetRequiredService<StudentServicesDbContext>();
        var logger = scope.ServiceProvider.GetService<ILogger<StudentServicesDbContext>>();

        if (!await context.Workflows.AnyAsync())
        {
            logger?.LogInformation("Seeding default workflow...");

            var workflow = new Workflow
            {
                Name = "Default Service Workflow",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        Order = 1,
                        StepKey = "personal_info",
                        Title = "Personal Information",
                        Description = "Please provide your personal details",
                        InputType = StepInputType.TextArea,
                        IsRequired = true,
                        ValidationRules = "{\"maxLength\":500}",
                        AvailableActions = new List<WorkflowStepAction>
                        {
                            new WorkflowStepAction { ActionKey = "next", Label = "Next", TriggersSubmission = false }
                        }
                    },
                    new WorkflowStep
                    {
                        Order = 2,
                        StepKey = "documents",
                        Title = "Upload Documents",
                        Description = "Upload required documents (PDF, JPG, PNG)",
                        InputType = StepInputType.FileUpload,
                        IsRequired = true,
                        ValidationRules = "{\"allowedExtensions\":[\".pdf\",\".jpg\",\".png\"],\"maxSizeMB\":5}",
                        AvailableActions = new List<WorkflowStepAction>
                        {
                            new WorkflowStepAction { ActionKey = "previous", Label = "Previous", TriggersSubmission = false },
                            new WorkflowStepAction { ActionKey = "submit", Label = "Submit Request", TriggersSubmission = true }
                        }
                    }
                }
            };

            context.Workflows.Add(workflow);
            await context.SaveChangesAsync();
        }

        if (!await context.Services.AnyAsync())
        {
            var defaultWorkflow = await context.Workflows.FirstOrDefaultAsync();
            if (defaultWorkflow != null)
            {
                var service = new Service
                {
                    Name = "Example Student Service",
                    Description = "This is an example service for demonstration.",
                    IsActive = true,
                    IsPaid = false,
                    Price = null,
                    Scope = new ServiceScope
                    {
                        IsGlobalStructural = true,
                        IsGlobalTemporal = true
                    },
                    WorkflowId = defaultWorkflow.Id
                };
                context.Services.Add(service);
                await context.SaveChangesAsync();
            }
        }

        logger?.LogInformation("StudentServices seeding completed.");
    }
}