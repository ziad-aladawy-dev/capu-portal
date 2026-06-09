using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.CrossCutting.Notifications;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.UniTests._Helpers;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Application;
using CapitalUniversity.Modules.StudentServices.Application.Validators;
using CapitalUniversity.Modules.StudentServices.Domain;
using CapitalUniversity.Modules.StudentServices.Repositories;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.UniTests.StudentServices;

/// <summary>
/// StudentServiceRequestService owns the full request lifecycle: cache-through
/// reads with per-row scope filtering, dynamic-field/document validation on
/// submit, fee integration, the workflow-vs-default transition graph, and the
/// bulk dispatcher's failure categorization. These tests pin every branch plus
/// the literal cache-key, default-status, and notification details so string /
/// boundary / conditional mutations are killed.
/// </summary>
public class StudentServiceRequestServiceTests
{
    private sealed class Ctx
    {
        public required StudentServiceRequestService Sut { get; init; }
        public required Mock<IUnitOfWork> Uow { get; init; }
        public required Mock<IStudentServiceRequestRepository> Requests { get; init; }
        public required Mock<IStudentServiceRepository> Services { get; init; }
        public required Mock<IWorkflowService> Workflows { get; init; }
        public required Mock<CapitalUniversity.Modules.Payments.Abstractions.Treasury.IFeeGenerationService> FeeGeneration { get; init; }
        public required Mock<IEffectiveScope> Scope { get; init; }
        public required Mock<ICacheService> Cache { get; init; }
        public required Mock<INotificationService> Notifications { get; init; }
        public required Mock<IAppLogger> Logger { get; init; }
    }

    private static Ctx Build()
    {
        var uow = new Mock<IUnitOfWork>();
        // Run the transactional critical section inline so the wrapped submit
        // logic executes under test (mirrors the in-memory provider behaviour).
        uow.Setup(u => u.ExecuteInSerializableTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));
        var requests = new Mock<IStudentServiceRequestRepository>();
        var services = new Mock<IStudentServiceRepository>();
        var workflows = new Mock<IWorkflowService>();
        var feeGeneration = new Mock<CapitalUniversity.Modules.Payments.Abstractions.Treasury.IFeeGenerationService>();
        // Default: no Treasury receipt mapping → null → legacy fee path runs.
        feeGeneration
            .Setup(f => f.GenerateFeeFromServiceAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        var scope = new Mock<IEffectiveScope>();
        var cache = new Mock<ICacheService>();
        var notifications = new Mock<INotificationService>();
        var logger = new Mock<IAppLogger>();

        // Default: caller can see everyone. Individual tests override to false.
        scope.Setup(s => s.CanAccessStudentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync(true);

        // List getters route through the stampede-protected GetOrSetAsync (a
        // default interface method); make the mock run the factory so the tests
        // exercise the real read path (cache miss → factory).
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<PagedResponse<StudentServiceRequestSummaryResponse>?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<PagedResponse<StudentServiceRequestSummaryResponse>?>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));

        var sut = new StudentServiceRequestService(
            uow.Object,
            requests.Object,
            services.Object,
            workflows.Object,
            feeGeneration.Object,
            scope.Object,
            cache.Object,
            notifications.Object,
            logger.Object,
            new TestLocalizationService(),
            new StudentServiceRequestValidators(
                new SubmitStudentServiceRequestValidator(),
                new CancelStudentServiceRequestValidator(),
                new ApproveStudentServiceRequestValidator(),
                new RejectStudentServiceRequestValidator(),
                new MoveRequestWorkflowStateValidator()));

        return new Ctx
        {
            Sut = sut,
            Uow = uow,
            Requests = requests,
            Services = services,
            Workflows = workflows,
            FeeGeneration = feeGeneration,
            Scope = scope,
            Cache = cache,
            Notifications = notifications,
            Logger = logger,
        };
    }

    private static string ExpectedCacheKey(Guid id) => $"student-service-request:object:{id:N}";

    private static StudentServiceRequest Req(
        Guid? id = null,
        Guid? studentId = null,
        Guid? serviceId = null,
        ServiceRequestStatus status = ServiceRequestStatus.Submitted) => new()
    {
        Id = id ?? Guid.NewGuid(),
        StudentId = studentId ?? Guid.NewGuid(),
        StudentServiceId = serviceId ?? Guid.NewGuid(),
        CurrentStatus = status,
        StudentService = new StudentService { Code = "svc", Name = LocalizedJson.Of("خدمة", "Service") },
    };

    private static StudentService Svc(
        bool isActive = true,
        bool requiresPayment = false,
        Guid? workflowId = null) => new()
    {
        Id = Guid.NewGuid(),
        Code = "svc",
        Name = LocalizedJson.Of("خدمة", "Service"),
        IsActive = isActive,
        RequiresPayment = requiresPayment,
        WorkflowDefinitionId = workflowId,
    };

    private static SubmitStudentServiceRequestRequest Submit(
        Guid serviceId,
        IReadOnlyList<ServiceFieldValueInput>? fields = null,
        IReadOnlyList<ServiceDocumentSubmissionInput>? files = null) => new()
    {
        StudentServiceId = serviceId,
        FieldValues = fields ?? Array.Empty<ServiceFieldValueInput>(),
        Files = files ?? Array.Empty<ServiceDocumentSubmissionInput>(),
    };

    private static void SetupList(
        Ctx c,
        IReadOnlyList<StudentServiceRequest> items,
        int total,
        Action<StudentServiceRequestListQuery, IReadOnlyCollection<ServiceRequestStatus>?>? capture = null)
    {
        c.Requests
            .Setup(r => r.ListAsync(
                It.IsAny<StudentServiceRequestListQuery>(),
                It.IsAny<IReadOnlyCollection<ServiceRequestStatus>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<StudentServiceRequestListQuery, IReadOnlyCollection<ServiceRequestStatus>?, CancellationToken>(
                (q, s, _) => capture?.Invoke(q, s))
            .ReturnsAsync((items, total));
    }

    // ---------------- GetByIdAsync ----------------

    [Fact]
    public async Task GetByIdAsync_CacheHit_ScopeAllowed_LocalizesAndSkipsRepository()
    {
        var c = Build();
        var id = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        c.Cache.Setup(x => x.GetAsync<StudentServiceRequestResponse>(ExpectedCacheKey(id), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new StudentServiceRequestResponse { Id = id, StudentId = studentId, ServiceName = LocalizedJson.Of("خدمة", "Service") });

        var result = await c.Sut.GetByIdAsync(id);

        result!.ServiceName.Should().Be("Service");
        c.Requests.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        c.Cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<StudentServiceRequestResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_CacheHit_ScopeDenied_ReturnsNull()
    {
        var c = Build();
        var id = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        c.Cache.Setup(x => x.GetAsync<StudentServiceRequestResponse>(ExpectedCacheKey(id), It.IsAny<CancellationToken>()))
              .ReturnsAsync(new StudentServiceRequestResponse { Id = id, StudentId = studentId });
        c.Scope.Setup(s => s.CanAccessStudentAsync(studentId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await c.Sut.GetByIdAsync(id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_NotFound_ReturnsNull()
    {
        var c = Build();
        var id = Guid.NewGuid();
        c.Cache.Setup(x => x.GetAsync<StudentServiceRequestResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((StudentServiceRequestResponse?)null);
        c.Requests.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync((StudentServiceRequest?)null);

        var result = await c.Sut.GetByIdAsync(id);

        result.Should().BeNull();
        c.Cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<StudentServiceRequestResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_Found_ScopeDenied_ReturnsNullAndDoesNotCache()
    {
        var c = Build();
        var id = Guid.NewGuid();
        var entity = Req(id);
        c.Cache.Setup(x => x.GetAsync<StudentServiceRequestResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((StudentServiceRequestResponse?)null);
        c.Requests.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        c.Scope.Setup(s => s.CanAccessStudentAsync(entity.StudentId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await c.Sut.GetByIdAsync(id);

        result.Should().BeNull();
        c.Cache.Verify(x => x.SetAsync(It.IsAny<string>(), It.IsAny<StudentServiceRequestResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_Found_MapsCachesAndLocalizes()
    {
        var c = Build();
        var id = Guid.NewGuid();
        var entity = Req(id);
        c.Cache.Setup(x => x.GetAsync<StudentServiceRequestResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((StudentServiceRequestResponse?)null);
        c.Requests.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await c.Sut.GetByIdAsync(id);

        result!.Id.Should().Be(id);
        result.ServiceName.Should().Be("Service");
        c.Cache.Verify(x => x.SetAsync(ExpectedCacheKey(id), It.IsAny<StudentServiceRequestResponse>(), TimeSpan.FromMinutes(5), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- ListAsync / ListAssigned / ListPending ----------------

    [Fact]
    public async Task ListAsync_FiltersOutOfScopeRows_KeepsRepoTotal()
    {
        var c = Build();
        var visibleStudent = Guid.NewGuid();
        var hiddenStudent = Guid.NewGuid();
        var visible = Req(studentId: visibleStudent);
        var hidden = Req(studentId: hiddenStudent);
        SetupList(c, new[] { visible, hidden }, total: 9);
        c.Scope.Setup(s => s.CanAccessStudentAsync(hiddenStudent, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await c.Sut.ListAsync(new StudentServiceRequestListQuery { Page = 1, PageSize = 25 });

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(visible.Id);
        result.Items[0].ServiceName.Should().Be("Service"); // localized
        result.TotalCount.Should().Be(9);
        result.PageSize.Should().Be(25);
    }

    [Fact]
    public async Task ListAssignedToStaffAsync_SetsAssignedStaffIdOnQuery()
    {
        var c = Build();
        var staffId = Guid.NewGuid();
        StudentServiceRequestListQuery? seen = null;
        SetupList(c, Array.Empty<StudentServiceRequest>(), total: 0, capture: (q, _) => seen = q);

        await c.Sut.ListAssignedToStaffAsync(staffId, new StudentServiceRequestListQuery());

        seen!.AssignedStaffId.Should().Be(staffId);
    }

    [Fact]
    public async Task ListPendingAsync_AppliesDefaultSortAndPendingStatusFilter()
    {
        var c = Build();
        IReadOnlyCollection<ServiceRequestStatus>? statuses = null;
        StudentServiceRequestListQuery? seen = null;
        SetupList(c, Array.Empty<StudentServiceRequest>(), total: 0, capture: (q, s) => { seen = q; statuses = s; });

        await c.Sut.ListPendingAsync(new StudentServiceRequestListQuery());

        seen!.SortBy.Should().Be("submittedAt");
        seen.SortAscending.Should().BeTrue();
        statuses.Should().BeEquivalentTo(new[]
        {
            ServiceRequestStatus.Submitted,
            ServiceRequestStatus.UnderReview,
            ServiceRequestStatus.WaitingPayment,
        });
    }

    [Fact]
    public async Task ListPendingAsync_DoesNotOverrideCallerSort()
    {
        var c = Build();
        StudentServiceRequestListQuery? seen = null;
        SetupList(c, Array.Empty<StudentServiceRequest>(), total: 0, capture: (q, _) => seen = q);

        await c.Sut.ListPendingAsync(new StudentServiceRequestListQuery { SortBy = "status", SortAscending = false });

        seen!.SortBy.Should().Be("status");
        seen.SortAscending.Should().BeFalse();
    }

    // ---------------- SubmitAsync ----------------

    [Fact]
    public async Task SubmitAsync_InvalidRequest_ThrowsValidationAndDoesNotPersist()
    {
        var c = Build();

        var act = () => c.Sut.SubmitAsync(Guid.NewGuid(), Submit(Guid.Empty)); // StudentServiceId empty

        await act.Should().ThrowAsync<ValidationException>();
        c.Requests.Verify(r => r.AddAsync(It.IsAny<StudentServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_ScopeDenied_ThrowsNotFound()
    {
        var c = Build();
        var studentId = Guid.NewGuid();
        c.Scope.Setup(s => s.CanAccessStudentAsync(studentId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => c.Sut.SubmitAsync(studentId, Submit(Guid.NewGuid()));

        (await act.Should().ThrowAsync<NotFoundException>())
            .Which.Message.Should().Contain(LocalizedKeys.StudentServices.ServiceNotFound);
    }

    [Fact]
    public async Task SubmitAsync_ServiceNotFound_ThrowsNotFound()
    {
        var c = Build();
        c.Services.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>())).ReturnsAsync((StudentService?)null);

        var act = () => c.Sut.SubmitAsync(Guid.NewGuid(), Submit(Guid.NewGuid()));

        (await act.Should().ThrowAsync<NotFoundException>())
            .Which.Message.Should().Contain(LocalizedKeys.StudentServices.ServiceNotFound);
    }

    [Fact]
    public async Task SubmitAsync_ServiceInactive_ThrowsConflict()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(Svc(isActive: false));

        var act = () => c.Sut.SubmitAsync(Guid.NewGuid(), Submit(serviceId));

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Message.Should().Contain(LocalizedKeys.StudentServices.ServiceInactive);
    }

    [Fact]
    public async Task SubmitAsync_RequiredFieldMissing_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var service = Svc();
        service.Fields.Add(new ServiceFieldDefinition { Id = Guid.NewGuid(), Name = "Language", FieldType = DynamicFieldType.Text, IsRequired = true });
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(service);

        var act = () => c.Sut.SubmitAsync(Guid.NewGuid(), Submit(serviceId));

        await act.Should().ThrowAsync<ValidationException>();
        c.Requests.Verify(r => r.AddAsync(It.IsAny<StudentServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SubmitAsync_DuplicateFieldValue_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(Svc());

        var req = Submit(serviceId, fields: new[]
        {
            new ServiceFieldValueInput { FieldDefinitionId = fieldId, Value = "a" },
            new ServiceFieldValueInput { FieldDefinitionId = fieldId, Value = "b" },
        });

        await c.Sut.Invoking(s => s.SubmitAsync(Guid.NewGuid(), req)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_InvalidNumberField_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var service = Svc();
        service.Fields.Add(new ServiceFieldDefinition { Id = fieldId, Name = "Copies", FieldType = DynamicFieldType.Number, IsRequired = false });
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(service);

        var req = Submit(serviceId, fields: new[] { new ServiceFieldValueInput { FieldDefinitionId = fieldId, Value = "not-a-number" } });

        await c.Sut.Invoking(s => s.SubmitAsync(Guid.NewGuid(), req)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_InvalidDropdownValue_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();
        var service = Svc();
        service.Fields.Add(new ServiceFieldDefinition { Id = fieldId, Name = "Lang", FieldType = DynamicFieldType.Dropdown, DropdownValues = "ar,en" });
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(service);

        var req = Submit(serviceId, fields: new[] { new ServiceFieldValueInput { FieldDefinitionId = fieldId, Value = "fr" } });

        await c.Sut.Invoking(s => s.SubmitAsync(Guid.NewGuid(), req)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_FileTooLarge_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var service = Svc();
        service.Documents.Add(new ServiceDocumentDefinition { Id = docId, Name = "Id", IsRequired = false, MaxFileSizeBytes = 10 });
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(service);

        var req = Submit(serviceId, files: new[]
        {
            new ServiceDocumentSubmissionInput { DocumentDefinitionId = docId, FileName = "id.pdf", FileSize = 100 },
        });

        await c.Sut.Invoking(s => s.SubmitAsync(Guid.NewGuid(), req)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_InvalidFileExtension_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var service = Svc();
        service.Documents.Add(new ServiceDocumentDefinition { Id = docId, Name = "Id", IsRequired = false, AllowedExtensions = "pdf" });
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(service);

        var req = Submit(serviceId, files: new[]
        {
            new ServiceDocumentSubmissionInput { DocumentDefinitionId = docId, FileName = "id.jpg", FileSize = 1 },
        });

        await c.Sut.Invoking(s => s.SubmitAsync(Guid.NewGuid(), req)).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_RequiredDocumentMissing_ThrowsValidation()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var service = Svc();
        service.Documents.Add(new ServiceDocumentDefinition { Id = Guid.NewGuid(), Name = "Id", IsRequired = true });
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(service);

        await c.Sut.Invoking(s => s.SubmitAsync(Guid.NewGuid(), Submit(serviceId))).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SubmitAsync_HappyPath_NoFee_PersistsSubmittedAndReturnsId()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(Svc());
        StudentServiceRequest? captured = null;
        c.Requests.Setup(r => r.AddAsync(It.IsAny<StudentServiceRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<StudentServiceRequest, CancellationToken>((e, _) => captured = e);

        var id = await c.Sut.SubmitAsync(Guid.NewGuid(), Submit(serviceId));

        captured.Should().NotBeNull();
        captured!.CurrentStatus.Should().Be(ServiceRequestStatus.Submitted);
        id.Should().Be(captured.Id);
        c.FeeGeneration.Verify(f => f.GenerateFeeFromServiceAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        c.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAsync_FeeService_CreatesTreasuryFeeAndMovesToWaitingPayment()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var feeId = Guid.NewGuid();
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(Svc(requiresPayment: true));
        c.FeeGeneration.Setup(f => f.GenerateFeeFromServiceAsync(
                studentId, It.IsAny<Guid>(), It.IsAny<int>(), "student-services", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(feeId);
        StudentServiceRequest? captured = null;
        c.Requests.Setup(r => r.AddAsync(It.IsAny<StudentServiceRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<StudentServiceRequest, CancellationToken>((e, _) => captured = e);

        await c.Sut.SubmitAsync(studentId, Submit(serviceId));

        captured!.CurrentStatus.Should().Be(ServiceRequestStatus.WaitingPayment);
        captured.PaymentReferenceId.Should().Be(feeId);
        c.Requests.Verify(r => r.Update(captured), Times.Once);
        c.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SubmitAsync_WorkflowInitialState_UsesResolvedStatus()
    {
        var c = Build();
        var serviceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        c.Services.Setup(s => s.GetByIdAsync(serviceId, true, It.IsAny<CancellationToken>())).ReturnsAsync(Svc(workflowId: workflowId));
        c.Workflows.Setup(w => w.ResolveInitialStateAsync(workflowId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new WorkflowStateResponse { Status = ServiceRequestStatus.UnderReview, IsInitial = true });
        StudentServiceRequest? captured = null;
        c.Requests.Setup(r => r.AddAsync(It.IsAny<StudentServiceRequest>(), It.IsAny<CancellationToken>()))
                  .Callback<StudentServiceRequest, CancellationToken>((e, _) => captured = e);

        await c.Sut.SubmitAsync(Guid.NewGuid(), Submit(serviceId));

        captured!.CurrentStatus.Should().Be(ServiceRequestStatus.UnderReview);
    }

    // ---------------- CancelAsync ----------------

    [Fact]
    public async Task CancelAsync_InvalidReason_ThrowsValidation()
    {
        var c = Build();

        var act = () => c.Sut.CancelAsync(Guid.NewGuid(), Guid.NewGuid(), new CancelStudentServiceRequestRequest { Reason = "" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task CancelAsync_NotFound_ThrowsNotFound()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync((StudentServiceRequest?)null);

        var act = () => c.Sut.CancelAsync(Guid.NewGuid(), requestId, new CancelStudentServiceRequestRequest { Reason = "x" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelAsync_WrongStudent_ThrowsNotFound()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var entity = Req(requestId, studentId: Guid.NewGuid(), status: ServiceRequestStatus.Submitted);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var act = () => c.Sut.CancelAsync(Guid.NewGuid(), requestId, new CancelStudentServiceRequestRequest { Reason = "x" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelAsync_ScopeDenied_ThrowsNotFound()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var entity = Req(requestId, studentId: studentId, status: ServiceRequestStatus.Submitted);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        c.Scope.Setup(s => s.CanAccessStudentAsync(studentId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => c.Sut.CancelAsync(studentId, requestId, new CancelStudentServiceRequestRequest { Reason = "x" });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(ServiceRequestStatus.UnderReview)]
    [InlineData(ServiceRequestStatus.Approved)]
    [InlineData(ServiceRequestStatus.Rejected)]
    [InlineData(ServiceRequestStatus.Completed)]
    [InlineData(ServiceRequestStatus.Cancelled)]
    public async Task CancelAsync_AfterProcessing_ThrowsConflict(ServiceRequestStatus status)
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var entity = Req(requestId, studentId: studentId, status: status);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var act = () => c.Sut.CancelAsync(studentId, requestId, new CancelStudentServiceRequestRequest { Reason = "x" });

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Message.Should().Contain(LocalizedKeys.StudentServices.CannotCancelAfterProcessing);
    }

    [Fact]
    public async Task CancelAsync_HappyPath_SetsCancelledAndInvalidatesCache()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var entity = Req(requestId, studentId: studentId, status: ServiceRequestStatus.Submitted);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await c.Sut.CancelAsync(studentId, requestId, new CancelStudentServiceRequestRequest { Reason = "changed mind" });

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.Cancelled);
        entity.CancellationReason.Should().Be("changed mind");
        entity.UpdatedAt.Should().NotBeNull();
        c.Requests.Verify(r => r.Update(entity), Times.Once);
        c.Cache.Verify(x => x.RemoveAsync(ExpectedCacheKey(requestId), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- ApproveAsync / RejectAsync ----------------

    [Fact]
    public async Task ApproveAsync_NotFound_ThrowsNotFound()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync((StudentServiceRequest?)null);

        var act = () => c.Sut.ApproveAsync(requestId, Guid.NewGuid(), new ApproveStudentServiceRequestRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ApproveAsync_InvalidTransition_ThrowsConflict()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.Submitted); // Submitted -> Approved not in default graph
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var act = () => c.Sut.ApproveAsync(requestId, Guid.NewGuid(), new ApproveStudentServiceRequestRequest());

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Message.Should().Contain(LocalizedKeys.StudentServices.InvalidTransition);
    }

    [Fact]
    public async Task ApproveAsync_HappyPath_SetsApprovedNotifiesInfoAndInvalidatesCache()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.UnderReview); // UnderReview -> Approved allowed
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await c.Sut.ApproveAsync(requestId, staffId, new ApproveStudentServiceRequestRequest { Note = "ok" });

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.Approved);
        entity.AssignedStaffId.Should().Be(staffId);
        entity.ProcessedAt.Should().NotBeNull();
        c.Notifications.Verify(n => n.EnqueueNotificationAsync(entity.StudentId, "Request approved", "ok", NotificationType.Info, It.IsAny<CancellationToken>()), Times.Once);
        c.Cache.Verify(x => x.RemoveAsync(ExpectedCacheKey(requestId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_HappyPath_SetsRejectedNotifiesWarning()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.UnderReview); // UnderReview -> Rejected allowed
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await c.Sut.RejectAsync(requestId, staffId, new RejectStudentServiceRequestRequest { Reason = "incomplete" });

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.Rejected);
        entity.RejectionReason.Should().Be("incomplete");
        entity.ProcessedAt.Should().NotBeNull();
        c.Notifications.Verify(n => n.EnqueueNotificationAsync(entity.StudentId, "Request rejected", "incomplete", NotificationType.Warning, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectAsync_InvalidReason_ThrowsValidation()
    {
        var c = Build();

        var act = () => c.Sut.RejectAsync(Guid.NewGuid(), Guid.NewGuid(), new RejectStudentServiceRequestRequest { Reason = "" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ---------------- MoveStateAsync (workflow + default graph) ----------------

    [Fact]
    public async Task MoveStateAsync_WorkflowResolvesTransition_Allows()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.Submitted);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        c.Services.Setup(s => s.GetByIdAsync(entity.StudentServiceId, false, It.IsAny<CancellationToken>())).ReturnsAsync(Svc(workflowId: workflowId));
        c.Workflows.Setup(w => w.ResolveTransitionAsync(workflowId, ServiceRequestStatus.Submitted, ServiceRequestStatus.Completed, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new WorkflowTransitionResponse { FromStatus = ServiceRequestStatus.Submitted, ToStatus = ServiceRequestStatus.Completed });

        await c.Sut.MoveStateAsync(requestId, Guid.NewGuid(), new MoveRequestWorkflowStateRequest { TargetStatus = ServiceRequestStatus.Completed });

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.Completed);
        entity.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MoveStateAsync_WorkflowMissingTransition_ThrowsConflict()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.Submitted);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        c.Services.Setup(s => s.GetByIdAsync(entity.StudentServiceId, false, It.IsAny<CancellationToken>())).ReturnsAsync(Svc(workflowId: workflowId));
        c.Workflows.Setup(w => w.ResolveTransitionAsync(workflowId, It.IsAny<ServiceRequestStatus>(), It.IsAny<ServiceRequestStatus>(), It.IsAny<CancellationToken>()))
                   .ReturnsAsync((WorkflowTransitionResponse?)null);

        var act = () => c.Sut.MoveStateAsync(requestId, Guid.NewGuid(), new MoveRequestWorkflowStateRequest { TargetStatus = ServiceRequestStatus.Completed });

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task MoveStateAsync_DefaultGraph_UnderReview_DoesNotSetProcessedAt()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.Submitted); // Submitted -> UnderReview allowed
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await c.Sut.MoveStateAsync(requestId, Guid.NewGuid(), new MoveRequestWorkflowStateRequest { TargetStatus = ServiceRequestStatus.UnderReview });

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.UnderReview);
        entity.ProcessedAt.Should().BeNull();
    }

    [Fact]
    public async Task MoveStateAsync_SameStatus_IsNoOpTransition_Succeeds()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.UnderReview);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await c.Sut.MoveStateAsync(requestId, Guid.NewGuid(), new MoveRequestWorkflowStateRequest { TargetStatus = ServiceRequestStatus.UnderReview });

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.UnderReview);
        // no-op transition path returns before the services lookup
        c.Services.Verify(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- ConfirmPaymentAsync ----------------

    [Fact]
    public async Task ConfirmPaymentAsync_NotFound_ThrowsNotFound()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync((StudentServiceRequest?)null);

        var act = () => c.Sut.ConfirmPaymentAsync(requestId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ConfirmPaymentAsync_NotWaitingPayment_IsNoOp()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.UnderReview);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await c.Sut.ConfirmPaymentAsync(requestId);

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.UnderReview);
        c.Requests.Verify(r => r.Update(It.IsAny<StudentServiceRequest>()), Times.Never);
        c.Uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_NoWorkflow_DefaultsToUnderReview()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.WaitingPayment);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        c.Services.Setup(s => s.GetByIdAsync(entity.StudentServiceId, false, It.IsAny<CancellationToken>())).ReturnsAsync(Svc());

        await c.Sut.ConfirmPaymentAsync(requestId);

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.UnderReview);
        c.Cache.Verify(x => x.RemoveAsync(ExpectedCacheKey(requestId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmPaymentAsync_WorkflowAutomaticTransition_UsesResolvedNext()
    {
        var c = Build();
        var requestId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var entity = Req(requestId, status: ServiceRequestStatus.WaitingPayment);
        c.Requests.Setup(r => r.GetByIdAsync(requestId, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        c.Services.Setup(s => s.GetByIdAsync(entity.StudentServiceId, false, It.IsAny<CancellationToken>())).ReturnsAsync(Svc(workflowId: workflowId));
        c.Workflows.Setup(w => w.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
                   .ReturnsAsync(new WorkflowDefinitionResponse
                   {
                       Id = workflowId,
                       Transitions = new[]
                       {
                           new WorkflowTransitionResponse
                           {
                               FromStatus = ServiceRequestStatus.WaitingPayment,
                               ToStatus = ServiceRequestStatus.Approved,
                               TransitionType = WorkflowTransitionType.Automatic,
                           },
                       },
                   });

        await c.Sut.ConfirmPaymentAsync(requestId);

        entity.CurrentStatus.Should().Be(ServiceRequestStatus.Approved);
    }

    // ---------------- BulkTransitionAsync ----------------

    [Fact]
    public async Task BulkTransitionAsync_CategorizesSuccessesAndFailures_AndDedupes()
    {
        var c = Build();
        var ok = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var conflicting = Guid.NewGuid();
        var staffId = Guid.NewGuid();

        c.Requests.Setup(r => r.GetByIdAsync(ok, false, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Req(ok, status: ServiceRequestStatus.Submitted));      // Submitted -> UnderReview OK
        c.Requests.Setup(r => r.GetByIdAsync(missing, false, It.IsAny<CancellationToken>()))
                  .ReturnsAsync((StudentServiceRequest?)null);                          // NotFound
        c.Requests.Setup(r => r.GetByIdAsync(conflicting, false, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(Req(conflicting, status: ServiceRequestStatus.Approved)); // Approved -> UnderReview Conflict

        var payload = new MoveRequestWorkflowStateRequest { TargetStatus = ServiceRequestStatus.UnderReview };

        var result = await c.Sut.BulkTransitionAsync(new[] { ok, missing, conflicting, ok }, staffId, payload);

        result.SucceededIds.Should().ContainSingle().Which.Should().Be(ok);
        result.Failures.Should().HaveCount(2);
        result.Failures.Single(f => f.Id == missing).Code.Should().Be(BulkFailureCodes.NotFound);
        result.Failures.Single(f => f.Id == conflicting).Code.Should().Be(BulkFailureCodes.InvalidTransition);
    }
}
