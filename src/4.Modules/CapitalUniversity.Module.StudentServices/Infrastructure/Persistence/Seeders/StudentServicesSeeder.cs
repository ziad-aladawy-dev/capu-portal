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

        var students = await coreDbContext.Students.OrderBy(s => s.CreatedAt).Take(10).ToListAsync();
        var staff = await coreDbContext.Staffs.Take(3).ToListAsync();
        var universityNode = await coreDbContext.StructureNodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Type == StructureNodeType.University);

        // ════════════════════════════════════════════════════════════
        //  1. WORKFLOWS
        // ════════════════════════════════════════════════════════════

        // ── 1a. Standard Service Workflow (Form → FileUpload → Review → Payment → Submit) ──
        var standardWf = new Workflow
        {
            Name = "Standard Service Workflow",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1, Title = "Personal Information",
                    Description = "Please fill your personal details",
                    StepType = WorkflowStepType.Form, IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new() { Order = 1, Label = "Full Name", FieldType = StepFieldType.Text, IsRequired = true },
                        new() { Order = 2, Label = "National ID", FieldType = StepFieldType.Text, IsRequired = true },
                        new() { Order = 3, Label = "Phone Number", FieldType = StepFieldType.Text, IsRequired = false },
                    },
                },
                new()
                {
                    Order = 2, Title = "Upload Documents",
                    Description = "Upload required documents",
                    StepType = WorkflowStepType.FileUpload, IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new() { Order = 1, Label = "ID Copy", FieldType = StepFieldType.File, IsRequired = true },
                        new() { Order = 2, Label = "Supporting Document", FieldType = StepFieldType.File, IsRequired = false },
                    },
                },
                new()
                {
                    Order = 3, Title = "Review",
                    Description = "Review your information",
                    StepType = WorkflowStepType.Review, IsRequired = true,
                },
                new()
                {
                    Order = 4, Title = "Payment",
                    Description = "Complete payment",
                    StepType = WorkflowStepType.Payment, IsRequired = true,
                },
                new()
                {
                    Order = 5, Title = "Submit",
                    Description = "Final submission",
                    StepType = WorkflowStepType.Submit, IsRequired = true,
                },
            },
        };
        dbContext.Workflows.Add(standardWf);

        // ── 1b. Simple Request Workflow (Form → Submit) ──
        var simpleWf = new Workflow
        {
            Name = "Simple Request Workflow",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1, Title = "Request Details",
                    Description = "Describe your request",
                    StepType = WorkflowStepType.Form, IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new() { Order = 1, Label = "Subject", FieldType = StepFieldType.Text, IsRequired = true },
                        new() { Order = 2, Label = "Description", FieldType = StepFieldType.TextArea, IsRequired = true },
                    },
                },
                new()
                {
                    Order = 2, Title = "Submit",
                    Description = "Confirm and submit",
                    StepType = WorkflowStepType.Submit, IsRequired = true,
                },
            },
        };
        dbContext.Workflows.Add(simpleWf);

        // ── 1c. Multi-Stage Approval Workflow (Form → Upload → Dept Review → Admin Review → Submit) ──
        var multiStageWf = new Workflow
        {
            Name = "Multi-Stage Approval Workflow",
            Steps = new List<WorkflowStep>
            {
                new()
                {
                    Order = 1, Title = "Application Form",
                    Description = "Fill in your details",
                    StepType = WorkflowStepType.Form, IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new() { Order = 1, Label = "Full Name", FieldType = StepFieldType.Text, IsRequired = true },
                        new() { Order = 2, Label = "Student ID", FieldType = StepFieldType.Text, IsRequired = true },
                        new() { Order = 3, Label = "Request Type", FieldType = StepFieldType.Select, IsRequired = true, OptionsJson = "[\"academic\",\"financial\",\"administrative\"]" },
                    },
                },
                new()
                {
                    Order = 2, Title = "Supporting Documents",
                    Description = "Upload supporting documents",
                    StepType = WorkflowStepType.FileUpload, IsRequired = true,
                    Fields = new List<WorkflowStepField>
                    {
                        new() { Order = 1, Label = "Supporting Document", FieldType = StepFieldType.File, IsRequired = true },
                    },
                },
                new()
                {
                    Order = 3, Title = "Department Review",
                    Description = "Department head reviews the request",
                    StepType = WorkflowStepType.Review, IsRequired = true,
                },
                new()
                {
                    Order = 4, Title = "Admin Review",
                    Description = "Administration final review",
                    StepType = WorkflowStepType.Review, IsRequired = true,
                },
                new()
                {
                    Order = 5, Title = "Submit",
                    Description = "Confirm submission",
                    StepType = WorkflowStepType.Submit, IsRequired = true,
                },
            },
        };
        dbContext.Workflows.Add(multiStageWf);

        await dbContext.SaveChangesAsync();
        logger?.LogInformation("3 workflows created.");

        // ════════════════════════════════════════════════════════════
        //  2. WORKFLOW STEP ACTIONS
        // ════════════════════════════════════════════════════════════

        var allSteps = await dbContext.WorkflowSteps.OrderBy(s => s.WorkflowId).ThenBy(s => s.Order).ToListAsync();
        foreach (var step in allSteps)
        {
            switch (step.StepType)
            {
                case WorkflowStepType.Form:
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "save_draft",
                        Label = "Save as Draft",
                        TriggersSubmission = false,
                    });
                    break;
                case WorkflowStepType.FileUpload:
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "upload",
                        Label = "Upload Files",
                        TriggersSubmission = false,
                    });
                    break;
                case WorkflowStepType.Review:
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "approve",
                        Label = "Approve",
                        TriggersSubmission = false,
                    });
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "reject",
                        Label = "Reject",
                        TriggersSubmission = false,
                    });
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "request_changes",
                        Label = "Request Changes",
                        TriggersSubmission = false,
                    });
                    break;
                case WorkflowStepType.Payment:
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "pay",
                        Label = "Proceed to Payment",
                        TriggersSubmission = false,
                    });
                    break;
                case WorkflowStepType.Submit:
                    dbContext.Set<WorkflowStepAction>().Add(new WorkflowStepAction
                    {
                        WorkflowStepId = step.Id,
                        ActionKey = "submit",
                        Label = "Submit Request",
                        TriggersSubmission = true,
                    });
                    break;
            }
        }

        await dbContext.SaveChangesAsync();
        logger?.LogInformation("Workflow step actions created.");

        // ════════════════════════════════════════════════════════════
        //  3. SERVICES
        // ════════════════════════════════════════════════════════════

        var reloadedWorkflows = await dbContext.Workflows.ToListAsync();
        var standardWfId = reloadedWorkflows.First(w => w.Name == "Standard Service Workflow").Id;
        var simpleWfId = reloadedWorkflows.First(w => w.Name == "Simple Request Workflow").Id;
        var multiStageWfId = reloadedWorkflows.First(w => w.Name == "Multi-Stage Approval Workflow").Id;

        // ── 3a. Transcript Request (paid, university-wide) ──
        var transcriptService = new Service
        {
            Name = "Transcript Request",
            Type = ServiceType.General,
            Description = "Request an official academic transcript",
            IsActive = true,
            IsPaid = true,
            Price = 150,
            IncludeDescendants = true,
            AcademicYearId = null,
            WorkflowId = standardWfId,
        };
        if (universityNode != null)
            transcriptService.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = universityNode.Id });

        // ── 3b. Leave of Absence (free, general) ──
        var leaveService = new Service
        {
            Name = "Leave of Absence Request",
            Type = ServiceType.General,
            Description = "Request a leave of absence for personal or academic reasons",
            IsActive = true,
            IsPaid = false,
            Price = null,
            IncludeDescendants = true,
            AcademicYearId = null,
            WorkflowId = simpleWfId,
        };
        if (universityNode != null)
            leaveService.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = universityNode.Id });

        // ── 3c. Grade Appeal (specialized, paid) ──
        var gradeAppealService = new Service
        {
            Name = "Grade Appeal",
            Type = ServiceType.Specialized,
            Description = "Appeal a course grade or exam result",
            IsActive = true,
            IsPaid = true,
            Price = 100,
            IncludeDescendants = true,
            AcademicYearId = null,
            WorkflowId = multiStageWfId,
        };
        if (universityNode != null)
            gradeAppealService.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = universityNode.Id });

        // ── 3d. Enrollment Certificate (paid, administrative) ──
        var enrollmentCertService = new Service
        {
            Name = "Enrollment Certificate",
            Type = ServiceType.Administrative,
            Description = "Request an official enrollment certificate",
            IsActive = true,
            IsPaid = true,
            Price = 75,
            IncludeDescendants = true,
            AcademicYearId = null,
            WorkflowId = standardWfId,
        };
        if (universityNode != null)
            enrollmentCertService.ScopeNodes.Add(new ServiceStructureNode { StructureNodeId = universityNode.Id });

        dbContext.Services.AddRange(transcriptService, leaveService, gradeAppealService, enrollmentCertService);

        try
        {
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("4 services created with scope nodes.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && sqlEx.Number == 547)
        {
            logger?.LogWarning("FK violation on scope nodes. Removing scope nodes and retrying.");
            transcriptService.ScopeNodes.Clear();
            leaveService.ScopeNodes.Clear();
            gradeAppealService.ScopeNodes.Clear();
            enrollmentCertService.ScopeNodes.Clear();
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Services saved without scope restrictions (global).");
        }

        // ════════════════════════════════════════════════════════════
        //  4. STUDENT REQUESTS + HISTORY + ATTACHMENTS
        // ════════════════════════════════════════════════════════════

        if (students.Count < 3)
        {
            logger?.LogWarning("Fewer than 3 students found. Skipping request seeding.");
            return;
        }

        var reloadedServices = await dbContext.Services.ToListAsync();
        var transcriptSvcId = reloadedServices.First(s => s.Name == "Transcript Request").Id;
        var leaveSvcId = reloadedServices.First(s => s.Name == "Leave of Absence Request").Id;
        var gradeSvcId = reloadedServices.First(s => s.Name == "Grade Appeal").Id;
        var enrollmentSvcId = reloadedServices.First(s => s.Name == "Enrollment Certificate").Id;
        var staff1 = staff.Count > 0 ? staff[0] : null;
        var staff2 = staff.Count > 1 ? staff[1] : null;

        var now = DateTime.UtcNow;

        // ── 4a. Draft transcript request (student 0) ──
        var draftRequest = new StudentRequest
        {
            StudentId = students[0].Id,
            ServiceId = transcriptSvcId,
            Status = RequestStatus.Draft,
            PaymentStatus = PaymentStatus.NotRequired,
            SubmittedData = @"{""full_name"":""" + students[0].Name + @""",""national_id"":""" + students[0].NationalId + @"""}",
            CurrentStepOrder = 0,
            HistoryEntries = new List<RequestHistoryEntry>
            {
                new() { Action = "Created", PerformedByUserId = students[0].Id, PerformedByRole = "Student", PerformedAt = now.AddHours(-2) },
            },
        };
        dbContext.StudentRequests.Add(draftRequest);

        // ── 4b. Submitted leave of absence (student 1) ──
        var submittedRequest = new StudentRequest
        {
            StudentId = students[1].Id,
            ServiceId = leaveSvcId,
            Status = RequestStatus.Pending,
            PaymentStatus = PaymentStatus.NotRequired,
            SubmittedData = @"{""subject"":""Leave of Absence"",""description"":""Requesting leave for spring semester due to medical reasons.""}",
            CurrentStepOrder = 2,
            SubmittedAt = now.AddDays(-3),
            HistoryEntries = new List<RequestHistoryEntry>
            {
                new() { Action = "Created", PerformedByUserId = students[1].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-3) },
                new() { Action = "Submitted", PerformedByUserId = students[1].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-3).AddHours(1) },
            },
        };
        dbContext.StudentRequests.Add(submittedRequest);

        // ── 4c. Completed grade appeal (student 2, with staff) ──
        var completedRequest = new StudentRequest
        {
            StudentId = students[2].Id,
            ServiceId = gradeSvcId,
            Status = RequestStatus.Completed,
            PaymentStatus = PaymentStatus.Paid,
            AmountPaid = 100,
            SubmittedData = @"{""full_name"":""" + students[2].Name + @""",""student_id"":""" + students[2].StudentCode + @""",""request_type"":""academic""}",
            CurrentStepOrder = 5,
            SubmittedAt = now.AddDays(-10),
            CompletedAt = now.AddDays(-5),
            AssignedToStaffId = staff1?.Id,
            AssignedAt = now.AddDays(-8),
            HistoryEntries = new List<RequestHistoryEntry>
            {
                new() { Action = "Created", PerformedByUserId = students[2].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-10) },
                new() { Action = "Submitted", PerformedByUserId = students[2].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-10).AddMinutes(30) },
                new() { Action = "Assigned", PerformedByUserId = staff1?.Id, PerformedByRole = "Staff", PerformedAt = now.AddDays(-8) },
                new() { Action = "Approved", PerformedByUserId = staff1?.Id, PerformedByRole = "Staff", PerformedAt = now.AddDays(-6) },
                new() { Action = "Completed", PerformedByUserId = staff1?.Id, PerformedByRole = "Staff", PerformedAt = now.AddDays(-5) },
            },
        };
        dbContext.StudentRequests.Add(completedRequest);

        // ── 4d. Rejected enrollment certificate (student 3, with staff) ──
        var rejectedRequest = new StudentRequest
        {
            StudentId = students[3].Id,
            ServiceId = enrollmentSvcId,
            Status = RequestStatus.Rejected,
            PaymentStatus = PaymentStatus.Refunded,
            AmountPaid = 75,
            SubmittedData = @"{""full_name"":""" + students[3].Name + @""",""national_id"":""" + students[3].NationalId + @"""}",
            CurrentStepOrder = 3,
            SubmittedAt = now.AddDays(-15),
            CompletedAt = now.AddDays(-12),
            AssignedToStaffId = staff2?.Id,
            AssignedAt = now.AddDays(-14),
            HistoryEntries = new List<RequestHistoryEntry>
            {
                new() { Action = "Created", PerformedByUserId = students[3].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-15) },
                new() { Action = "Submitted", PerformedByUserId = students[3].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-15).AddHours(2) },
                new() { Action = "Assigned", PerformedByUserId = staff2?.Id, PerformedByRole = "Staff", PerformedAt = now.AddDays(-14) },
                new() { Action = "Rejected", Comment = "Insufficient documentation. Please provide a valid national ID.", PerformedByUserId = staff2?.Id, PerformedByRole = "Staff", PerformedAt = now.AddDays(-12) },
            },
        };
        dbContext.StudentRequests.Add(rejectedRequest);

        // ── 4e. Under review transcript request (student 4, assigned) ──
        var underReviewRequest = new StudentRequest
        {
            StudentId = students[4].Id,
            ServiceId = transcriptSvcId,
            Status = RequestStatus.UnderReview,
            PaymentStatus = PaymentStatus.Paid,
            AmountPaid = 150,
            SubmittedData = @"{""full_name"":""" + students[4].Name + @""",""national_id"":""" + students[4].NationalId + @""",""phone_number"":""01000000123""}",
            CurrentStepOrder = 3,
            SubmittedAt = now.AddDays(-2),
            AssignedToStaffId = staff1?.Id,
            AssignedAt = now.AddDays(-1),
            HistoryEntries = new List<RequestHistoryEntry>
            {
                new() { Action = "Created", PerformedByUserId = students[4].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-2) },
                new() { Action = "Submitted", PerformedByUserId = students[4].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-2).AddMinutes(45) },
                new() { Action = "Payment Completed", PerformedByUserId = students[4].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-2).AddHours(1) },
                new() { Action = "Assigned", PerformedByUserId = staff1?.Id, PerformedByRole = "Staff", PerformedAt = now.AddDays(-1) },
            },
        };
        dbContext.StudentRequests.Add(underReviewRequest);

        // ── 4f. Payment pending transcript request (student 5) ──
        var paymentPendingRequest = new StudentRequest
        {
            StudentId = students[5].Id,
            ServiceId = transcriptSvcId,
            Status = RequestStatus.PaymentPending,
            PaymentStatus = PaymentStatus.Pending,
            SubmittedData = @"{""full_name"":""" + students[5].Name + @""",""national_id"":""" + students[5].NationalId + @"""}",
            CurrentStepOrder = 4,
            SubmittedAt = now.AddDays(-1),
            HistoryEntries = new List<RequestHistoryEntry>
            {
                new() { Action = "Created", PerformedByUserId = students[5].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-1) },
                new() { Action = "Submitted", PerformedByUserId = students[5].Id, PerformedByRole = "Student", PerformedAt = now.AddDays(-1).AddMinutes(15) },
            },
        };
        dbContext.StudentRequests.Add(paymentPendingRequest);

        await dbContext.SaveChangesAsync();
        logger?.LogInformation("6 student requests created with history entries.");

        // ════════════════════════════════════════════════════════════
        //  5. REQUEST ATTACHMENTS
        // ════════════════════════════════════════════════════════════

        var savedRequests = await dbContext.StudentRequests.OrderBy(r => r.CreatedAt).ToListAsync();
        if (savedRequests.Count >= 2)
        {
            dbContext.Set<RequestAttachment>().AddRange(
                new RequestAttachment
                {
                    StudentRequestId = savedRequests[0].Id,
                    StepKey = "upload_documents",
                    FileName = "national_id.pdf",
                    FilePath = "/uploads/requests/" + savedRequests[0].Id + "/national_id.pdf",
                    FileSize = 245_760,
                    MimeType = "application/pdf",
                },
                new RequestAttachment
                {
                    StudentRequestId = savedRequests[1].Id,
                    StepKey = "request_details",
                    FileName = "medical_report.pdf",
                    FilePath = "/uploads/requests/" + savedRequests[1].Id + "/medical_report.pdf",
                    FileSize = 512_000,
                    MimeType = "application/pdf",
                },
                new RequestAttachment
                {
                    StudentRequestId = savedRequests[2].Id,
                    StepKey = "supporting_documents",
                    FileName = "grade_sheet.pdf",
                    FilePath = "/uploads/requests/" + savedRequests[2].Id + "/grade_sheet.pdf",
                    FileSize = 180_224,
                    MimeType = "application/pdf",
                }
            );

            await dbContext.SaveChangesAsync();
            logger?.LogInformation("3 request attachments created.");
        }

        logger?.LogInformation("StudentServices seeding completed.");
    }
}
