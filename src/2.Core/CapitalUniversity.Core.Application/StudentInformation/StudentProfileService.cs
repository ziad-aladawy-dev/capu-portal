using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.StudentInformation;
using CapitalUniversity.Core.Abstractions.StudentInformation.DTOs;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.StudentInformation;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.StudentInformation;

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
    private static readonly TimeSpan StandardTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan SensitiveTtl = TimeSpan.FromMinutes(2);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStudentProfileRecordRepository _records;
    private readonly IValidator<UpsertStudentProfileRecordRequest> _upsertValidator;
    private readonly IValidator<VerifyStudentProfileRecordRequest> _verifyValidator;
    private readonly ICacheService _cache;

    public StudentProfileService(
        IUnitOfWork unitOfWork,
        IStudentProfileRecordRepository records,
        IValidator<UpsertStudentProfileRecordRequest> upsertValidator,
        IValidator<VerifyStudentProfileRecordRequest> verifyValidator,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _records = records;
        _upsertValidator = upsertValidator;
        _verifyValidator = verifyValidator;
        _cache = cache;
    }

    public async Task<StudentProfileRecordResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(id);
        var cached = await _cache.GetAsync<StudentProfileRecordResponse>(key, cancellationToken);
        if (cached is not null) return cached;

        var record = await _records.GetByIdAsync(id, cancellationToken);
        if (record is null) return null;

        var dto = ToResponse(record);
        await _cache.SetAsync(key, dto, TtlFor(dto), cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<StudentProfileRecordResponse>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var records = await _records.GetForStudentAsync(studentId, cancellationToken);
        return records.Select(ToResponse).ToList();
    }

    public async Task<StudentProfileRecordResponse?> GetForStudentCategoryAsync(Guid studentId, StudentProfileCategory category, string? customCategoryKey = null, CancellationToken cancellationToken = default)
    {
        var record = await _records.GetForStudentCategoryAsync(studentId, category, customCategoryKey ?? string.Empty, cancellationToken);
        return record is null ? null : ToResponse(record);
    }

    public async Task<Guid> UpsertAsync(Guid studentId, UpsertStudentProfileRecordRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _upsertValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
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
        return record.Id;
    }

    public async Task VerifyAsync(Guid id, VerifyStudentProfileRecordRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await _verifyValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var record = await _records.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Profile record not found.");

        record.VerifiedBy = request.VerifiedBy;
        record.VerifiedAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;
        _records.Update(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var record = await _records.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Profile record not found.");
        _records.Delete(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _cache.RemoveAsync(CacheKey(id), cancellationToken);
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
