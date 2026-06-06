using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation.DTOs;
using CapitalUniversity.Modules.Student.Domain;
using CapitalUniversity.Modules.Student.Repositories;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Modules.Student.Application;

/// <summary>
/// Owns the flexible JSON-backed profile records. Upsert keys on
/// <c>(StudentId, Category, CustomCategoryKey)</c> so each category exists at
/// most once per student — the rest of the system can rely on a canonical
/// record per category. Sensitive records get a shorter cache TTL so any
/// reasonable inflight stale view cannot outlive an emergency revocation
/// by long.
/// </summary>
public class StudentProfileService : IStudentProfileService
{
    internal const string CacheKeyPrefix = "studentprofile:object:";
    // Collection caches (per-student list + per-category record) keyed by a
    // version stamp; any record mutation rotates it. Correctness against access
    // revocation is preserved by the per-caller CanAccessStudentAsync gate that
    // runs OUTSIDE the cache on every read.
    internal const string CollectionVersionKey = "studentprofile:coll:ver";
    internal const string StudentListKeyPrefix = "studentprofile:student:";
    internal const string CategoryKeyPrefix = "studentprofile:student-cat:";
    private const string ProfileRecordNotFound = LocalizedKeys.StudentInformation.ProfileRecordNotFound;
    private static readonly TimeSpan StandardTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SensitiveTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentProfileRecordRepository _records;
    private readonly IValidator<UpsertStudentProfileRecordRequest> _upsertValidator;
    private readonly IValidator<VerifyStudentProfileRecordRequest> _verifyValidator;
    private readonly ICacheService _cache;
    private readonly IEffectiveScope _scope;

    public StudentProfileService(
        IUnitOfWork unitOfWork,
        IStudentProfileRecordRepository records,
        IValidator<UpsertStudentProfileRecordRequest> upsertValidator,
        IValidator<VerifyStudentProfileRecordRequest> verifyValidator,
        ICacheService cache,
        IEffectiveScope scope)
    {
        _unitOfWork = unitOfWork;
        _records = records;
        _upsertValidator = upsertValidator;
        _verifyValidator = verifyValidator;
        _cache = cache;
        _scope = scope;
    }

    public async Task<StudentProfileRecordResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);

        // P1.1 — sensitive records hit shared cache; scope is enforced on every
        // read against the cached StudentId. Out-of-scope returns null → 404.
        var cached = await _cache.GetAsync<StudentProfileRecordResponse>(key, cancellationToken);
        if (cached is not null)
        {
            return await _scope.CanAccessStudentAsync(cached.StudentId, cancellationToken) ? cached : null;
        }

        var record = await _records.GetByIdAsync(id, cancellationToken);
        if (record is null) return null;

        if (!await _scope.CanAccessStudentAsync(record.StudentId, cancellationToken)) return null;

        var dto = ToResponse(record);
        await _cache.SetAsync(key, dto, TtlFor(dto), cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<StudentProfileRecordResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<StudentProfileRecordResponse>();
        }

        // Cached scope-neutral per student under the collection version, behind
        // the per-caller scope gate above. Stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var cached = await _cache.GetOrSetAsync<List<StudentProfileRecordResponse>>(
            $"{StudentListKeyPrefix}{studentId:N}:{version}",
            async ct => (await _records.GetForStudentAsync(studentId, ct)).Select(ToResponse).ToList(),
            StandardTtl,
            cancellationToken);

        return new List<StudentProfileRecordResponse>(cached ?? new List<StudentProfileRecordResponse>());
    }

    public async Task<StudentProfileRecordResponse?> GetForStudentCategoryAsync(Guid studentId, StudentProfileCategory category, string? customCategoryKey = null, CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken)) return null;

        // Cached by (student, category, customKey) under the collection version.
        // not-found is not cached. Stampede-protected.
        var version = await GetCollectionVersionAsync(cancellationToken);
        var custom = customCategoryKey ?? string.Empty;
        return await _cache.GetOrSetAsync<StudentProfileRecordResponse>(
            $"{CategoryKeyPrefix}{studentId:N}:{(int)category}:{custom}:{version}",
            async ct =>
            {
                var record = await _records.GetForStudentCategoryAsync(studentId, category, custom, ct);
                return record is null ? null : ToResponse(record);
            },
            StandardTtl,
            cancellationToken);
    }

    private async Task<string> GetCollectionVersionAsync(CancellationToken cancellationToken)
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey, cancellationToken);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", VersionTtl, cancellationToken);
        return "0";
    }

    private Task BumpCollectionVersionAsync(CancellationToken cancellationToken) =>
        _cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl, cancellationToken);

    public async Task<Guid> UpsertAsync(Guid studentId, UpsertStudentProfileRecordRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _upsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            throw new NotFoundException(LocalizedKeys.StudentInformation.StudentNotFound);
        }

        var customKey = request.Category == StudentProfileCategory.Custom
            ? (request.CustomCategoryKey ?? string.Empty)
            : string.Empty;

        var existing = await _records.GetForStudentCategoryAsync(studentId, request.Category, customKey, cancellationToken);
        if (existing is not null)
        {
            existing.SchemaVersion = request.SchemaVersion;
            existing.DataJson = request.DataJson;
            existing.IsSensitive = request.IsSensitive;
            // Re-verification required after data changes — clear stamps so
            // operators don't see stale "verified" badges over edited content.
            existing.VerifiedBy = null;
            existing.VerifiedAt = null;
            existing.UpdatedAt = DateTime.UtcNow;
            _records.Update(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _cache.RemoveAsync(CacheKey(existing.Id), cancellationToken);
            await BumpCollectionVersionAsync(cancellationToken);
            return existing.Id;
        }

        var record = new StudentProfileRecord
        {
            StudentId = studentId,
            Category = request.Category,
            CustomCategoryKey = customKey,
            SchemaVersion = request.SchemaVersion,
            DataJson = request.DataJson,
            IsSensitive = request.IsSensitive,
        };
        await _records.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        // A new record must surface in the student's list / category reads.
        await BumpCollectionVersionAsync(cancellationToken);
        return record.Id;
    }

    public async Task VerifyAsync(Guid studentId, Guid id, VerifyStudentProfileRecordRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _verifyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var record = await _records.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(ProfileRecordNotFound);

        // C1 — ownership guard. A record loaded by id whose StudentId disagrees
        // with the route's studentId is reported identically to a missing record
        // so an attacker cannot probe for cross-student record ids.
        if (record.StudentId != studentId)
        {
            throw new NotFoundException(ProfileRecordNotFound);
        }

        if (!await _scope.CanAccessStudentAsync(record.StudentId, cancellationToken))
        {
            throw new NotFoundException(ProfileRecordNotFound);
        }

        record.VerifiedBy = request.VerifiedBy;
        record.VerifiedAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;
        _records.Update(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid studentId, Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _records.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(ProfileRecordNotFound);

        // C1 — same ownership guard as VerifyAsync.
        if (record.StudentId != studentId)
        {
            throw new NotFoundException(ProfileRecordNotFound);
        }

        if (!await _scope.CanAccessStudentAsync(record.StudentId, cancellationToken))
        {
            throw new NotFoundException(ProfileRecordNotFound);
        }

        _records.Delete(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
        await BumpCollectionVersionAsync(cancellationToken);
    }

    public async Task<BulkActionResult> BatchUpsertAsync(Guid studentId, IReadOnlyList<UpsertStudentProfileRecordRequest> records, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(records.Count);
        var failures = new List<BulkActionFailure>();

        for (var idx = 0; idx < records.Count; idx++)
        {
            // Stable per-index pseudo-id so the caller can correlate failures
            // with the original record (no entity id exists pre-upsert).
            var slot = SlotId(idx);
            try
            {
                var id = await UpsertAsync(studentId, records[idx], cancellationToken);
                succeeded.Add(id);
            }
            catch (NotFoundException ex)
            {
                failures.Add(new BulkActionFailure { Id = slot, Code = BulkFailureCodes.NotFound, Message = ex.Message });
            }
            catch (ValidationException ex)
            {
                failures.Add(new BulkActionFailure { Id = slot, Code = BulkFailureCodes.Validation, Message = ex.Message });
            }
            catch (ConflictException ex)
            {
                failures.Add(new BulkActionFailure { Id = slot, Code = BulkFailureCodes.Conflict, Message = ex.Message });
            }
        }

        return BulkActionResult.From(succeeded, failures);
    }

    public async Task<BulkActionResult> BatchVerifyAsync(Guid studentId, IReadOnlyList<Guid> recordIds, Guid verifiedBy, CancellationToken cancellationToken = default)
    {
        var succeeded = new List<Guid>(recordIds.Count);
        var failures = new List<BulkActionFailure>();
        var verifyReq = new VerifyStudentProfileRecordRequest { VerifiedBy = verifiedBy };

        foreach (var id in recordIds.Distinct())
        {
            try
            {
                await VerifyAsync(studentId, id, verifyReq, cancellationToken);
                succeeded.Add(id);
            }
            catch (NotFoundException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.NotFound, Message = ex.Message });
            }
            catch (ValidationException ex)
            {
                failures.Add(new BulkActionFailure { Id = id, Code = BulkFailureCodes.Validation, Message = ex.Message });
            }
        }

        return BulkActionResult.From(succeeded, failures);
    }

    /// <summary>
    /// Synthetic per-index id used in <see cref="BatchUpsertAsync"/> failures
    /// so callers can correlate a failing record with its position in the
    /// request (no entity id exists for a record that never persisted).
    /// </summary>
    private static Guid SlotId(int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(index).CopyTo(bytes, 12);
        return new Guid(bytes);
    }

    internal static string CacheKey(Guid id) => $"{CacheKeyPrefix}{id:N}";

    private static TimeSpan TtlFor(StudentProfileRecordResponse dto) =>
        dto.IsSensitive ? SensitiveTtl : StandardTtl;

    internal static StudentProfileRecordResponse ToResponse(StudentProfileRecord record) => new()
    {
        Id = record.Id,
        StudentId = record.StudentId,
        Category = record.Category,
        CustomCategoryKey = record.CustomCategoryKey,
        SchemaVersion = record.SchemaVersion,
        DataJson = record.DataJson,
        VerifiedBy = record.VerifiedBy,
        VerifiedAt = record.VerifiedAt,
        IsSensitive = record.IsSensitive,
        CreatedAt = record.CreatedAt,
        UpdatedAt = record.UpdatedAt,
    };
}
