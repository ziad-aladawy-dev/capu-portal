using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.Repositories;
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
/// StudentServiceService contract: owns the configurable service catalog.
/// Reads are cache-through with a culture-agnostic stored payload localized on
/// the read path; writes normalize bilingual JSON, enforce code uniqueness,
/// apply sparse updates, replace field/document collections wholesale, soft
/// delete, and invalidate the per-object cache entry. These tests pin every
/// branch and the literal cache-key / default-currency / lower-casing details.
/// </summary>
public class StudentServiceServiceTests
{
    private static (
        StudentServiceService Service,
        Mock<IStudentServiceRepository> Repo,
        Mock<IUnitOfWork> Uow,
        Mock<ICacheService> Cache,
        Mock<IAppLogger> Logger)
        Build()
    {
        var repo = new Mock<IStudentServiceRepository>();
        var uow = new Mock<IUnitOfWork>();
        var cache = new Mock<ICacheService>();
        var logger = new Mock<IAppLogger>();

        // The list getters route through the stampede-protected GetOrSetAsync
        // (a default interface method). Make the mock run the factory so the
        // tests exercise the real read path (cache miss → factory).
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<StudentServiceSummaryResponse>?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<List<StudentServiceSummaryResponse>?>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));
        cache.Setup(c => c.GetOrSetAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<PagedResponse<StudentServiceSummaryResponse>?>>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<PagedResponse<StudentServiceSummaryResponse>?>>, TimeSpan?, CancellationToken>(
                (_, factory, _, ct) => factory(ct));

        var sut = new StudentServiceService(
            uow.Object,
            repo.Object,
            new StudentServiceValidators(
                new CreateStudentServiceValidator(),
                new UpdateStudentServiceValidator()),
            cache.Object,
            logger.Object,
            new TestLocalizationService());

        return (sut, repo, uow, cache, logger);
    }

    private static string ExpectedCacheKey(Guid id) => $"student-service:object:{id:N}";

    private static CreateStudentServiceRequest ValidCreate(
        string code = "transcript-request",
        bool requiresPayment = false,
        IReadOnlyList<Guid>? roleIds = null) => new()
    {
        Code = code,
        Name = "Transcript",
        Description = "Official transcript",
        RequiresPayment = requiresPayment,
        EstimatedProcessingDays = 3,
        AllowedProcessingRoleIds = roleIds ?? Array.Empty<Guid>(),
        Fields = Array.Empty<CreateServiceFieldDefinitionRequest>(),
        Documents = Array.Empty<CreateServiceDocumentDefinitionRequest>(),
    };

    private static StudentService ExistingService(Guid? id = null, bool isActive = true) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Code = "transcript-request",
        Name = LocalizedJson.Of("كشف", "Transcript"),
        Description = LocalizedJson.Of("وصف", "Description"),
        IsActive = isActive,
        RequiresPayment = true,
        EstimatedProcessingDays = 3,
        AllowedProcessingRoleIdsCsv = string.Empty,
    };

    // ---------------- CreateAsync ----------------

    [Fact]
    public async Task CreateAsync_HappyPath_NormalizesPersistsAndReturnsNewId()
    {
        var (sut, repo, uow, _, logger) = Build();
        repo.Setup(r => r.ExistsByCodeAsync("transcript-request", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        StudentService? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StudentService>(), It.IsAny<CancellationToken>()))
            .Callback<StudentService, CancellationToken>((s, _) => captured = s);

        var id = await sut.CreateAsync(ValidCreate());

        captured.Should().NotBeNull();
        captured!.Code.Should().Be("transcript-request");
        captured.Name.Should().Be(LocalizedJson.Of("Transcript", "Transcript"));
        captured.Description.Should().Be(LocalizedJson.Of("Official transcript", "Official transcript"));
        captured.IsActive.Should().BeTrue();
        id.Should().Be(captured.Id);
        repo.Verify(r => r.AddAsync(captured, It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        logger.Verify(l => l.LogInfoAsync(
            It.Is<string>(m => m.Contains("created") && m.Contains("transcript-request")),
            nameof(StudentServiceService),
            null,
            It.IsAny<Dictionary<string, object>>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NoRoleIds_StoresEmptyCsv()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        StudentService? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StudentService>(), It.IsAny<CancellationToken>()))
            .Callback<StudentService, CancellationToken>((s, _) => captured = s);

        await sut.CreateAsync(ValidCreate());

        captured!.AllowedProcessingRoleIdsCsv.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithRoleIds_JoinsAsCommaSeparatedDForm()
    {
        var (sut, repo, _, _, _) = Build();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        repo.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        StudentService? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StudentService>(), It.IsAny<CancellationToken>()))
            .Callback<StudentService, CancellationToken>((s, _) => captured = s);

        await sut.CreateAsync(ValidCreate(roleIds: new[] { r1, r2 }));

        captured!.AllowedProcessingRoleIdsCsv.Should().Be($"{r1:D},{r2:D}");
    }

    [Fact]
    public async Task CreateAsync_AddsFieldsAndDocuments_LowercasingExtensions()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.ExistsByCodeAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        StudentService? captured = null;
        repo.Setup(r => r.AddAsync(It.IsAny<StudentService>(), It.IsAny<CancellationToken>()))
            .Callback<StudentService, CancellationToken>((s, _) => captured = s);

        var request = ValidCreate();
        request.Fields = new[]
        {
            new CreateServiceFieldDefinitionRequest { Name = "Copies", Label = "Number of copies", FieldType = DynamicFieldType.Number, DisplayOrder = 0 },
        };
        request.Documents = new[]
        {
            new CreateServiceDocumentDefinitionRequest { Name = "NationalId", Label = "ID", AllowedExtensions = "PDF,JPG", MaxFileSizeBytes = 1024, DisplayOrder = 0 },
        };

        await sut.CreateAsync(request);

        captured!.Fields.Should().HaveCount(1);
        captured.Documents.Should().HaveCount(1);
        captured.Documents.Single().AllowedExtensions.Should().Be("pdf,jpg");
    }

    [Fact]
    public async Task CreateAsync_InvalidRequest_ThrowsValidationAndDoesNotPersist()
    {
        var (sut, repo, uow, _, _) = Build();
        var request = ValidCreate();
        request.Code = ""; // fails NotEmpty

        var act = () => sut.CreateAsync(request);

        await act.Should().ThrowAsync<ValidationException>();
        repo.Verify(r => r.AddAsync(It.IsAny<StudentService>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ThrowsConflictAndDoesNotPersist()
    {
        var (sut, repo, uow, _, _) = Build();
        repo.Setup(r => r.ExistsByCodeAsync("transcript-request", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => sut.CreateAsync(ValidCreate());

        (await act.Should().ThrowAsync<ConflictException>())
            .Which.Message.Should().Contain(LocalizedKeys.StudentServices.ServiceCodeInUse);
        repo.Verify(r => r.AddAsync(It.IsAny<StudentService>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------- GetByIdAsync ----------------

    [Fact]
    public async Task GetByIdAsync_CacheHit_LocalizesAndSkipsRepository()
    {
        var (sut, repo, _, cache, _) = Build();
        var id = Guid.NewGuid();
        cache.Setup(c => c.GetAsync<StudentServiceResponse>(ExpectedCacheKey(id), It.IsAny<CancellationToken>()))
             .ReturnsAsync(new StudentServiceResponse { Id = id, Name = LocalizedJson.Of("كشف", "Transcript") });

        var result = await sut.GetByIdAsync(id);

        result!.Name.Should().Be("Transcript");
        repo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<StudentServiceResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_LoadsMapsCachesAndLocalizes()
    {
        var (sut, repo, _, cache, _) = Build();
        var id = Guid.NewGuid();
        cache.Setup(c => c.GetAsync<StudentServiceResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((StudentServiceResponse?)null);
        var entity = ExistingService(id);
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await sut.GetByIdAsync(id);

        result!.Id.Should().Be(id);
        result.Name.Should().Be("Transcript");
        repo.Verify(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.SetAsync(ExpectedCacheKey(id), It.IsAny<StudentServiceResponse>(), TimeSpan.FromMinutes(15), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNullAndDoesNotCache()
    {
        var (sut, repo, _, cache, _) = Build();
        var id = Guid.NewGuid();
        cache.Setup(c => c.GetAsync<StudentServiceResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((StudentServiceResponse?)null);
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync((StudentService?)null);

        var result = await sut.GetByIdAsync(id);

        result.Should().BeNull();
        cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<StudentServiceResponse>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByIdAsync_OrdersFieldsAndDocumentsByDisplayOrder()
    {
        var (sut, repo, _, cache, _) = Build();
        var id = Guid.NewGuid();
        cache.Setup(c => c.GetAsync<StudentServiceResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((StudentServiceResponse?)null);
        var entity = ExistingService(id);
        entity.Fields = new List<ServiceFieldDefinition>
        {
            new() { Name = "B", Label = LocalizedJson.Of("ب", "B"), DisplayOrder = 2 },
            new() { Name = "A", Label = LocalizedJson.Of("أ", "A"), DisplayOrder = 1 },
        };
        entity.Documents = new List<ServiceDocumentDefinition>
        {
            new() { Name = "D2", Label = LocalizedJson.Of("د٢", "D2"), DisplayOrder = 5 },
            new() { Name = "D1", Label = LocalizedJson.Of("د١", "D1"), DisplayOrder = 1 },
        };
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await sut.GetByIdAsync(id);

        result!.Fields.Select(f => f.Name).Should().ContainInOrder("A", "B");
        result.Documents.Select(d => d.Name).Should().ContainInOrder("D1", "D2");
    }

    [Fact]
    public async Task GetByIdAsync_CacheMiss_RoundTripsRoleIdsFromCsv()
    {
        var (sut, repo, _, cache, _) = Build();
        var id = Guid.NewGuid();
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        cache.Setup(c => c.GetAsync<StudentServiceResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((StudentServiceResponse?)null);
        var entity = ExistingService(id);
        entity.AllowedProcessingRoleIdsCsv = $"{r1:D},{r2:D}";
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var result = await sut.GetByIdAsync(id);

        result!.AllowedProcessingRoleIds.Should().Equal(r1, r2);
    }

    // ---------------- GetAllAsync / GetAvailableAsync ----------------

    [Fact]
    public async Task GetAllAsync_ReturnsPagingMetadataAndLocalizedItems()
    {
        var (sut, repo, _, _, _) = Build();
        var items = new List<StudentService> { ExistingService(), ExistingService() };
        repo.Setup(r => r.ListAsync(2, 10, "trans", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 25));

        var result = await sut.GetAllAsync(2, 10, "trans", true);

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(25);
        result.Items.Should().HaveCount(2);
        result.Items[0].Name.Should().Be("Transcript");
        repo.Verify(r => r.ListAsync(2, 10, "trans", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAvailableAsync_ReturnsActiveServicesLocalized()
    {
        var (sut, repo, _, _, _) = Build();
        repo.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudentService> { ExistingService(), ExistingService(), ExistingService() });

        var result = await sut.GetAvailableAsync();

        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Transcript");
        repo.Verify(r => r.GetActiveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- UpdateAsync ----------------

    [Fact]
    public async Task UpdateAsync_NotFound_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync((StudentService?)null);

        var act = () => sut.UpdateAsync(id, new UpdateStudentServiceRequest());

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_Invalid_Throws()
    {
        var (sut, _, _, _, _) = Build();
        var request = new UpdateStudentServiceRequest { Name = new string('x', 513) }; // exceeds 512

        var act = () => sut.UpdateAsync(Guid.NewGuid(), request);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateAsync_SparseUpdate_OnlyAppliesSetFields()
    {
        var (sut, repo, uow, cache, _) = Build();
        var id = Guid.NewGuid();
        var entity = ExistingService(id);
        entity.RequiresPayment = true;
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await sut.UpdateAsync(id, new UpdateStudentServiceRequest { Name = "Updated" });

        entity.Name.Should().Be(LocalizedJson.Of("Updated", "Updated"));
        entity.RequiresPayment.Should().BeTrue();   // untouched
        entity.UpdatedAt.Should().NotBeNull();
        repo.Verify(r => r.Update(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(ExpectedCacheKey(id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NullFields_LeavesExistingFieldsIntact()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        var entity = ExistingService(id);
        entity.Fields = new List<ServiceFieldDefinition> { new() { Name = "Keep", Label = LocalizedJson.Of("ا", "Keep"), DisplayOrder = 0 } };
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await sut.UpdateAsync(id, new UpdateStudentServiceRequest { Fields = null });

        entity.Fields.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_ProvidedFieldsAndDocuments_ReplaceWholesale()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        var entity = ExistingService(id);
        entity.Fields = new List<ServiceFieldDefinition>
        {
            new() { Name = "Old1", Label = LocalizedJson.Of("ا", "Old1"), DisplayOrder = 0 },
            new() { Name = "Old2", Label = LocalizedJson.Of("ب", "Old2"), DisplayOrder = 1 },
        };
        entity.Documents = new List<ServiceDocumentDefinition>
        {
            new() { Name = "OldDoc", Label = LocalizedJson.Of("ا", "OldDoc"), AllowedExtensions = "pdf", MaxFileSizeBytes = 1, DisplayOrder = 0 },
        };
        repo.Setup(r => r.GetByIdAsync(id, true, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await sut.UpdateAsync(id, new UpdateStudentServiceRequest
        {
            Fields = new[] { new CreateServiceFieldDefinitionRequest { Name = "New", Label = "New", FieldType = DynamicFieldType.Text, DisplayOrder = 0 } },
            Documents = Array.Empty<CreateServiceDocumentDefinitionRequest>(),
        });

        entity.Fields.Should().HaveCount(1);
        entity.Fields.Single().Name.Should().Be("New");
        entity.Documents.Should().BeEmpty();
    }

    // ---------------- ToggleStatusAsync ----------------

    [Fact]
    public async Task ToggleStatusAsync_NotFound_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync((StudentService?)null);

        var act = () => sut.ToggleStatusAsync(id, true);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ToggleStatusAsync_SameStatus_IsNoOp()
    {
        var (sut, repo, uow, cache, _) = Build();
        var id = Guid.NewGuid();
        var entity = ExistingService(id, isActive: true);
        repo.Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await sut.ToggleStatusAsync(id, isActive: true);

        repo.Verify(r => r.Update(It.IsAny<StudentService>()), Times.Never);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ToggleStatusAsync_StatusChanged_PersistsAndInvalidatesCache()
    {
        var (sut, repo, uow, cache, _) = Build();
        var id = Guid.NewGuid();
        var entity = ExistingService(id, isActive: true);
        repo.Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await sut.ToggleStatusAsync(id, isActive: false);

        entity.IsActive.Should().BeFalse();
        entity.UpdatedAt.Should().NotBeNull();
        repo.Verify(r => r.Update(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(ExpectedCacheKey(id), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------- DeleteAsync ----------------

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        var (sut, repo, _, _, _) = Build();
        var id = Guid.NewGuid();
        repo.Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync((StudentService?)null);

        var act = () => sut.DeleteAsync(id);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_DeactivatesAndInvalidatesCache()
    {
        var (sut, repo, uow, cache, _) = Build();
        var id = Guid.NewGuid();
        var entity = ExistingService(id, isActive: true);
        repo.Setup(r => r.GetByIdAsync(id, false, It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        await sut.DeleteAsync(id);

        entity.IsDeleted.Should().BeTrue();
        entity.IsActive.Should().BeFalse();
        entity.UpdatedAt.Should().NotBeNull();
        repo.Verify(r => r.Update(entity), Times.Once);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveAsync(ExpectedCacheKey(id), It.IsAny<CancellationToken>()), Times.Once);
    }
}
