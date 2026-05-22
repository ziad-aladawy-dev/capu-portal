using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CapitalUniversity.Modules.CourseOffering.Repositories;
using FluentValidation;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.CourseOffering.Application;

/// <summary>
/// Owns the create + update lifecycle for <see cref="CourseOfferingEntity"/>.
/// Scope is enforced against the offering's <c>StructureNodeId</c> — out-of-scope
/// reads return <c>null</c> and mutations throw <see cref="NotFoundException"/>
/// to avoid leaking existence (consistent with InvoiceService / AcademicPlanService).
///
/// <para>
/// Deliberately narrow: no registration orchestration, no schedule conflict
/// logic, no fee / transcript coupling. Future Registration / Scheduling
/// modules consume an offering by id and apply their own policies.
/// </para>
/// </summary>
public class CourseOfferingService : ICourseOfferingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICourseOfferingRepository _offerings;
    private readonly IValidator<CreateCourseOfferingRequest> _createValidator;
    private readonly IValidator<UpdateCourseOfferingRequest> _updateValidator;
    private readonly IEffectiveScope _scope;

    public CourseOfferingService(
        IUnitOfWork unitOfWork,
        ICourseOfferingRepository offerings,
        IValidator<CreateCourseOfferingRequest> createValidator,
        IValidator<UpdateCourseOfferingRequest> updateValidator,
        IEffectiveScope scope)
    {
        _unitOfWork = unitOfWork;
        _offerings = offerings;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _scope = scope;
    }

    public async Task<CourseOfferingResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await _offerings.GetByIdAsync(id, cancellationToken);
        if (offering is null) return null;
        if (!await _scope.CanAccessStructureNodeAsync(offering.StructureNodeId, cancellationToken)) return null;
        return ToResponse(offering);
    }

    public async Task<IReadOnlyList<CourseOfferingResponse>> GetForNodeSemesterAsync(
        Guid structureNodeId,
        Guid semesterId,
        OfferingStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStructureNodeAsync(structureNodeId, cancellationToken))
        {
            return Array.Empty<CourseOfferingResponse>();
        }

        var offerings = await _offerings.GetForNodeSemesterAsync(structureNodeId, semesterId, status, cancellationToken);
        return offerings.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<CourseOfferingResponse>> GetForCourseAsync(
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken = default)
    {
        var offerings = await _offerings.GetForCourseAsync(courseId, semesterId, cancellationToken);
        if (offerings.Count == 0) return Array.Empty<CourseOfferingResponse>();

        // Cross-node query — filter each row by per-node visibility so an admin
        // sees only the offerings their scope grants. Avoids broadcasting that
        // a course is running under a node they cannot otherwise see.
        var visible = new List<CourseOfferingResponse>(offerings.Count);
        foreach (var offering in offerings)
        {
            if (await _scope.CanAccessStructureNodeAsync(offering.StructureNodeId, cancellationToken))
            {
                visible.Add(ToResponse(offering));
            }
        }
        return visible;
    }

    public async Task<Guid> CreateAsync(CreateCourseOfferingRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        if (!await _scope.CanAccessStructureNodeAsync(request.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.Courses.StructureNodeNotFound);
        }

        if (await _offerings.SectionExistsAsync(request.CourseId, request.SemesterId, request.StructureNodeId, request.SectionCode, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.CourseOfferings.SectionInUse);
        }

        var offering = new CourseOfferingEntity
        {
            CourseId = request.CourseId,
            SemesterId = request.SemesterId,
            StructureNodeId = request.StructureNodeId,
            SectionCode = request.SectionCode,
            Status = request.Status,
            RegistrationState = request.RegistrationState,
            ExternalSystemId = request.ExternalSystemId,
        };
        offering.InitializeCapacity(request.Capacity);

        await _offerings.AddAsync(offering, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return offering.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateCourseOfferingRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) throw ValidationFrom(validation);

        var offering = await LoadForWriteAsync(id, cancellationToken);

        // Each Apply* helper carries its own "only act if the field was sent"
        // guard and its own exception-translation logic. Order matters:
        // Status before RegistrationState (the registration-state guard reads
        // post-transition Status). The rest are independent.
        await ApplySectionCodeAsync(offering, request, cancellationToken);
        ApplyCapacity(offering, request);
        ApplyStatusChange(offering, request);
        ApplyRegistrationStateChange(offering, request);
        ApplyExternalSyncMetadata(offering, request);

        offering.UpdatedAt = DateTime.UtcNow;
        _offerings.Update(offering);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Section code is the only field whose uniqueness has to be re-verified
    /// against siblings before mutation. Skips if the request didn't include
    /// the field or the value is unchanged.
    /// </summary>
    private async Task ApplySectionCodeAsync(CourseOfferingEntity offering, UpdateCourseOfferingRequest request, CancellationToken cancellationToken)
    {
        if (request.SectionCode is null || request.SectionCode == offering.SectionCode) return;

        if (await _offerings.SectionExistsAsync(offering.CourseId, offering.SemesterId, offering.StructureNodeId, request.SectionCode, cancellationToken))
        {
            throw new ConflictException(LocalizedKeys.CourseOfferings.SectionInUse);
        }
        offering.SectionCode = request.SectionCode;
    }

    /// <summary>
    /// Capacity changes route through the entity's <c>AdjustCapacity</c>
    /// invariant guard. The entity throws <see cref="InvalidOperationException"/>
    /// when the new value falls below the current registered count; we surface
    /// that as a conflict so the controller maps to 409 with the localized key.
    /// </summary>
    private static void ApplyCapacity(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.Capacity.HasValue) return;

        try { offering.AdjustCapacity(request.Capacity.Value); }
        catch (InvalidOperationException) { throw new ConflictException(LocalizedKeys.CourseOfferings.CapacityBelowCount); }
    }

    /// <summary>
    /// Status transition. Same-state requests are silent no-ops so a partial
    /// update payload echoing the current status doesn't trip the lifecycle
    /// guards. Illegal transitions (e.g. Closed → Open) surface as conflict.
    /// </summary>
    private static void ApplyStatusChange(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.Status.HasValue || request.Status.Value == offering.Status) return;

        try { ApplyStatus(offering, request.Status.Value); }
        catch (InvalidOperationException) { throw new ConflictException(LocalizedKeys.CourseOfferings.IllegalStateTransition); }
    }

    /// <summary>
    /// Registration-state transition. Same-state is a silent no-op (matches
    /// the Status path). The entity's per-state guards (e.g. Open requires
    /// <c>Status == Open</c>) surface as conflict.
    /// </summary>
    private static void ApplyRegistrationStateChange(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (!request.RegistrationState.HasValue || request.RegistrationState.Value == offering.RegistrationState) return;

        try { ApplyRegistrationState(offering, request.RegistrationState.Value); }
        catch (InvalidOperationException) { throw new ConflictException(LocalizedKeys.CourseOfferings.IllegalStateTransition); }
    }

    /// <summary>
    /// Passive external-sync metadata. No invariants — just per-field
    /// "set when sent" semantics. Kept as its own helper so the orchestration
    /// reads as one statement per concern.
    /// </summary>
    private static void ApplyExternalSyncMetadata(CourseOfferingEntity offering, UpdateCourseOfferingRequest request)
    {
        if (request.ExternalSystemId is not null) offering.ExternalSystemId = request.ExternalSystemId;
        if (request.ExternalSyncedAt.HasValue) offering.ExternalSyncedAt = request.ExternalSyncedAt;
    }

    internal static CourseOfferingResponse ToResponse(CourseOfferingEntity o) => new()
    {
        Id = o.Id,
        CourseId = o.CourseId,
        SemesterId = o.SemesterId,
        StructureNodeId = o.StructureNodeId,
        SectionCode = o.SectionCode,
        Capacity = o.Capacity,
        RegisteredCount = o.RegisteredCount,
        Status = o.Status,
        RegistrationState = o.RegistrationState,
        ExternalSystemId = o.ExternalSystemId,
        ExternalSyncedAt = o.ExternalSyncedAt,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
    };

    private static ValidationException ValidationFrom(FluentValidation.Results.ValidationResult result) =>
        new(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));

    public async Task CloseRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await LoadForWriteAsync(id, cancellationToken);
        offering.Close();
        _offerings.Update(offering);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task OpenRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var offering = await LoadForWriteAsync(id, cancellationToken);
        offering.Reopen();
        _offerings.Update(offering);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Fetch a tracked offering by id and require the caller's scope to cover
    /// its owning structure node. Single helper so every write path uses the
    /// same miss / out-of-scope handling — both map to <see cref="NotFoundException"/>
    /// with the same key to avoid leaking existence (P1.1 in the project's
    /// remediation plan).
    /// </summary>
    private async Task<CourseOfferingEntity> LoadForWriteAsync(Guid id, CancellationToken cancellationToken)
    {
        var offering = await _offerings.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);

        if (!await _scope.CanAccessStructureNodeAsync(offering.StructureNodeId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.CourseOfferings.NotFound);
        }

        offering.EnsureMutable();

        return offering;
    }

    private static void ApplyStatus(CourseOfferingEntity offering, OfferingStatus target)
    {
        switch (target)
        {
            case OfferingStatus.Open:      offering.Activate(); break;
            case OfferingStatus.Closed:    offering.Close();    break;
            case OfferingStatus.Cancelled: offering.Cancel();   break;
            // Reverting to Draft from any non-Draft state is not a legal
            // transition — surface as the same illegal-transition conflict.
            case OfferingStatus.Draft:     throw new InvalidOperationException("Cannot revert an offering to draft.");
            default:                       throw new InvalidOperationException($"Unknown offering status: {target}.");
        }
    }

    private static void ApplyRegistrationState(CourseOfferingEntity offering, RegistrationState target)
    {
        switch (target)
        {
            case RegistrationState.Open:     offering.OpenRegistration();  break;
            case RegistrationState.Closed:   offering.CloseRegistration(); break;
            case RegistrationState.Waitlist: offering.SetWaitlist();       break;
            default: throw new InvalidOperationException($"Unknown registration state: {target}.");
        }
    }
}
