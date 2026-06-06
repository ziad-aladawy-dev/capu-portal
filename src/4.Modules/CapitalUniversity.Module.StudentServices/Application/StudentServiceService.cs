using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Domain;
using CapitalUniversity.Modules.StudentServices.Repositories;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.StudentServices.Application;

/// <summary>
/// Bundle of validators used by <see cref="StudentServiceService"/>. Mirrors
/// the <c>ScheduleSlotValidators</c> pattern — keeps the service constructor
/// inside the project's parameter cap.
/// </summary>
public sealed record StudentServiceValidators(
    IValidator<CreateStudentServiceRequest> Create,
    IValidator<UpdateStudentServiceRequest> Update);

/// <summary>
/// Owns the service catalog. Active services are surfaced to students via
/// <see cref="GetAvailableAsync"/>; admins see the full catalog through
/// <see cref="GetAllAsync"/>. Configuration changes (fields, documents,
/// workflow) are sparse-update through <see cref="UpdateAsync"/>; soft delete
/// hides services without losing historical request lineage.
/// </summary>
public class StudentServiceService : IStudentServiceService
{
    internal const string CacheKeyPrefix = "student-service:object:";
    // Catalog list caches keyed by a version stamp; any service mutation rotates
    // it. The catalog is global (no per-user scope), so lists are shared.
    internal const string CollectionVersionKey = "student-service:coll:ver";
    internal const string AvailableKeyPrefix = "student-service:available:";
    internal const string AllKeyPrefix = "student-service:all:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentServiceRepository _services;
    private readonly StudentServiceValidators _validators;
    private readonly ICacheService _cache;
    private readonly IAppLogger _logger;
    private readonly ILocalizationService _localization;

    public StudentServiceService(
        IUnitOfWork unitOfWork,
        IStudentServiceRepository services,
        StudentServiceValidators validators,
        ICacheService cache,
        IAppLogger logger,
        ILocalizationService localization)
    {
        _unitOfWork = unitOfWork;
        _services = services;
        _validators = validators;
        _cache = cache;
        _logger = logger;
        _localization = localization;
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    public async Task<StudentServiceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Cached payload stays culture-agnostic (bilingual JSON in
        // Name / Description / Label). Localize runs on the read path so a
        // single cache entry serves every Accept-Language — same convention
        // as ScheduleSlotService.
        var cached = await _cache.GetAsync<StudentServiceResponse>(CacheKey(id), cancellationToken);
        if (cached is not null) return Localize(cached);

        var service = await _services.GetByIdAsync(id, includeChildren: true, cancellationToken);
        if (service is null) return null;

        var dto = MapToResponse(service);
        await _cache.SetAsync(CacheKey(id), dto, CacheTtl, cancellationToken);
        return Localize(dto);
    }

    public async Task<PagedResponse<StudentServiceSummaryResponse>> GetAllAsync(
        int page,
        int pageSize,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        // Cache the raw (culture-neutral) summary page under the collection
        // version + query params; localize onto copies on read. Stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var key = $"{AllKeyPrefix}{version}:{page}:{pageSize}:{search ?? string.Empty}:{isActive?.ToString() ?? string.Empty}";
        var cached = await _cache.GetOrSetAsync<PagedResponse<StudentServiceSummaryResponse>>(
            key,
            async ct =>
            {
                var (items, total) = await _services.ListAsync(page, pageSize, search, isActive, ct);
                return new PagedResponse<StudentServiceSummaryResponse>
                {
                    Items = items.Select(MapToSummary).ToList(),
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = total,
                };
            },
            CacheTtl,
            cancellationToken)
            ?? new PagedResponse<StudentServiceSummaryResponse> { Items = new List<StudentServiceSummaryResponse>(), Page = page, PageSize = pageSize };

        return new PagedResponse<StudentServiceSummaryResponse>
        {
            Items = cached.Items.Select(LocalizeSummaryCopy).ToList(),
            Page = cached.Page,
            PageSize = cached.PageSize,
            TotalCount = cached.TotalCount,
        };
    }

    public async Task<IReadOnlyList<StudentServiceSummaryResponse>> GetAvailableAsync(CancellationToken cancellationToken = default)
    {
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<StudentServiceSummaryResponse>>(
            $"{AvailableKeyPrefix}{version}",
            async ct => (await _services.GetActiveAsync(ct)).Select(MapToSummary).ToList(),
            CacheTtl,
            cancellationToken);
        return (cached ?? new List<StudentServiceSummaryResponse>()).Select(LocalizeSummaryCopy).ToList();
    }

    /// <summary>
    /// Decode bilingual JSON fields (Name, Description, child Label rows) into
    /// the current culture's display text. Applied as a read-time projection
    /// so the cached DTO can safely be shared across cultures.
    /// </summary>
    private StudentServiceResponse Localize(StudentServiceResponse response) => new()
    {
        Id = response.Id,
        Code = response.Code,
        Name = _localization.Get<string>(response.Name),
        Description = string.IsNullOrEmpty(response.Description) ? string.Empty : _localization.Get<string>(response.Description),
        IsActive = response.IsActive,
        RequiresPayment = response.RequiresPayment,
        FeeType = response.FeeType,
        FeeAmount = response.FeeAmount,
        Currency = response.Currency,
        EstimatedProcessingDays = response.EstimatedProcessingDays,
        AllowedProcessingRoleIds = response.AllowedProcessingRoleIds,
        WorkflowDefinitionId = response.WorkflowDefinitionId,
        Fields = response.Fields.Select(f => new ServiceFieldDefinitionResponse
        {
            Id = f.Id,
            Name = f.Name,
            Label = _localization.Get<string>(f.Label),
            FieldType = f.FieldType,
            IsRequired = f.IsRequired,
            DisplayOrder = f.DisplayOrder,
            MinLength = f.MinLength,
            MaxLength = f.MaxLength,
            MinValue = f.MinValue,
            MaxValue = f.MaxValue,
            DropdownValues = f.DropdownValues,
        }).ToList(),
        Documents = response.Documents.Select(d => new ServiceDocumentDefinitionResponse
        {
            Id = d.Id,
            Name = d.Name,
            Label = _localization.Get<string>(d.Label),
            IsRequired = d.IsRequired,
            DisplayOrder = d.DisplayOrder,
            AllowedExtensions = d.AllowedExtensions,
            MaxFileSizeBytes = d.MaxFileSizeBytes,
        }).ToList(),
        CreatedAt = response.CreatedAt,
        UpdatedAt = response.UpdatedAt,
    };

    private StudentServiceSummaryResponse LocalizeSummary(StudentServiceSummaryResponse response)
    {
        response.Name = _localization.Get<string>(response.Name);
        return response;
    }

    /// <summary>Localize onto a NEW summary so cached list entries are never mutated.</summary>
    private StudentServiceSummaryResponse LocalizeSummaryCopy(StudentServiceSummaryResponse s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = _localization.Get<string>(s.Name),
        IsActive = s.IsActive,
        RequiresPayment = s.RequiresPayment,
        FeeAmount = s.FeeAmount,
        Currency = s.Currency,
        EstimatedProcessingDays = s.EstimatedProcessingDays,
    };

    private async Task<string> GetCollectionVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpCollectionVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    public async Task<Guid> CreateAsync(CreateStudentServiceRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Create.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        if (await _services.ExistsByCodeAsync(request.Code, excludeId: null, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.StudentServices.ServiceCodeInUse);
        }

        var service = new StudentService
        {
            Code = request.Code,
            Name = LocalizedJson.Normalize(request.Name),
            Description = LocalizedJson.Normalize(request.Description),
            IsActive = true,
            RequiresPayment = request.RequiresPayment,
            FeeType = request.FeeType,
            FeeAmount = request.FeeAmount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "EGP" : request.Currency,
            EstimatedProcessingDays = request.EstimatedProcessingDays,
            AllowedProcessingRoleIdsCsv = JoinRoleIds(request.AllowedProcessingRoleIds),
            WorkflowDefinitionId = request.WorkflowDefinitionId,
        };
        foreach (var f in request.Fields)
        {
            service.Fields.Add(MapFieldToEntity(f));
        }
        foreach (var d in request.Documents)
        {
            service.Documents.Add(MapDocumentToEntity(d));
        }
        await _services.AddAsync(service, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // A new service must surface in catalog list / available reads.
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"StudentService created code={service.Code}",
            nameof(StudentServiceService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["StudentServiceId"] = service.Id,
                ["Code"] = service.Code,
            });
        return service.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateStudentServiceRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _validators.Update.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var service = await _services.GetByIdAsync(id, includeChildren: true, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.ServiceNotFound);

        if (request.Name is not null) service.Name = LocalizedJson.Normalize(request.Name);
        if (request.Description is not null) service.Description = LocalizedJson.Normalize(request.Description);
        if (request.RequiresPayment.HasValue) service.RequiresPayment = request.RequiresPayment.Value;
        if (request.FeeType is not null) service.FeeType = request.FeeType;
        if (request.FeeAmount.HasValue) service.FeeAmount = request.FeeAmount.Value;
        if (!string.IsNullOrWhiteSpace(request.Currency)) service.Currency = request.Currency;
        if (request.EstimatedProcessingDays.HasValue) service.EstimatedProcessingDays = request.EstimatedProcessingDays.Value;
        if (request.AllowedProcessingRoleIds is not null) service.AllowedProcessingRoleIdsCsv = JoinRoleIds(request.AllowedProcessingRoleIds);
        if (request.WorkflowDefinitionId.HasValue) service.WorkflowDefinitionId = request.WorkflowDefinitionId.Value;

        // Fields + Documents replacement is wholesale — the admin UI sends
        // the full desired set so the diff is clean and no client has to
        // reconcile mismatched ids.
        if (request.Fields is not null)
        {
            service.Fields.Clear();
            foreach (var f in request.Fields) service.Fields.Add(MapFieldToEntity(f));
        }
        if (request.Documents is not null)
        {
            service.Documents.Clear();
            foreach (var d in request.Documents) service.Documents.Add(MapDocumentToEntity(d));
        }

        service.UpdatedAt = DateTime.UtcNow;
        _services.Update(service);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"StudentService updated id={id}",
            nameof(StudentServiceService),
            context: null,
            metadata: new Dictionary<string, object> { ["StudentServiceId"] = id });
    }

    public async Task ToggleStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var service = await _services.GetByIdAsync(id, includeChildren: false, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.ServiceNotFound);

        if (service.IsActive == isActive) return;
        service.IsActive = isActive;
        service.UpdatedAt = DateTime.UtcNow;
        _services.Update(service);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"StudentService toggled id={id} active={isActive}",
            nameof(StudentServiceService),
            context: null,
            metadata: new Dictionary<string, object>
            {
                ["StudentServiceId"] = id,
                ["IsActive"] = isActive,
            });
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _services.GetByIdAsync(id, includeChildren: false, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.StudentServices.ServiceNotFound);

        // Soft delete via the ISoftDeletable flag. The query filter hides
        // the row from subsequent reads; outstanding requests still
        // reference the row by id so they remain auditable.
        service.IsDeleted = true;
        service.IsActive = false;
        service.UpdatedAt = DateTime.UtcNow;
        _services.Update(service);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);

        await _logger.LogInfoAsync(
            $"StudentService soft-deleted id={id}",
            nameof(StudentServiceService),
            context: null,
            metadata: new Dictionary<string, object> { ["StudentServiceId"] = id });
    }

    internal static StudentServiceResponse MapToResponse(StudentService s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        Description = s.Description,
        IsActive = s.IsActive,
        RequiresPayment = s.RequiresPayment,
        FeeType = s.FeeType,
        FeeAmount = s.FeeAmount,
        Currency = s.Currency,
        EstimatedProcessingDays = s.EstimatedProcessingDays,
        AllowedProcessingRoleIds = SplitRoleIds(s.AllowedProcessingRoleIdsCsv),
        WorkflowDefinitionId = s.WorkflowDefinitionId,
        Fields = s.Fields.OrderBy(f => f.DisplayOrder).Select(MapFieldToResponse).ToList(),
        Documents = s.Documents.OrderBy(d => d.DisplayOrder).Select(MapDocumentToResponse).ToList(),
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };

    private static StudentServiceSummaryResponse MapToSummary(StudentService s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Name = s.Name,
        IsActive = s.IsActive,
        RequiresPayment = s.RequiresPayment,
        FeeAmount = s.FeeAmount,
        Currency = s.Currency,
        EstimatedProcessingDays = s.EstimatedProcessingDays,
    };

    private static ServiceFieldDefinitionResponse MapFieldToResponse(ServiceFieldDefinition f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Label = f.Label,
        FieldType = f.FieldType,
        IsRequired = f.IsRequired,
        DisplayOrder = f.DisplayOrder,
        MinLength = f.MinLength,
        MaxLength = f.MaxLength,
        MinValue = f.MinValue,
        MaxValue = f.MaxValue,
        DropdownValues = f.DropdownValues,
    };

    private static ServiceDocumentDefinitionResponse MapDocumentToResponse(ServiceDocumentDefinition d) => new()
    {
        Id = d.Id,
        Name = d.Name,
        Label = d.Label,
        IsRequired = d.IsRequired,
        DisplayOrder = d.DisplayOrder,
        AllowedExtensions = d.AllowedExtensions,
        MaxFileSizeBytes = d.MaxFileSizeBytes,
    };

    private static ServiceFieldDefinition MapFieldToEntity(CreateServiceFieldDefinitionRequest f) => new()
    {
        Name = f.Name,
        Label = LocalizedJson.Normalize(f.Label),
        FieldType = f.FieldType,
        IsRequired = f.IsRequired,
        DisplayOrder = f.DisplayOrder,
        MinLength = f.MinLength,
        MaxLength = f.MaxLength,
        MinValue = f.MinValue,
        MaxValue = f.MaxValue,
        DropdownValues = f.DropdownValues ?? string.Empty,
    };

    private static ServiceDocumentDefinition MapDocumentToEntity(CreateServiceDocumentDefinitionRequest d) => new()
    {
        Name = d.Name,
        Label = LocalizedJson.Normalize(d.Label),
        IsRequired = d.IsRequired,
        DisplayOrder = d.DisplayOrder,
        AllowedExtensions = (d.AllowedExtensions ?? string.Empty).ToLowerInvariant(),
        MaxFileSizeBytes = d.MaxFileSizeBytes,
    };

    private static string JoinRoleIds(IReadOnlyList<Guid> ids) =>
        ids.Count == 0 ? string.Empty : string.Join(',', ids.Select(g => g.ToString("D")));

    private static IReadOnlyList<Guid> SplitRoleIds(string csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<Guid>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .Select(Guid.Parse)
                 .ToList();

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
