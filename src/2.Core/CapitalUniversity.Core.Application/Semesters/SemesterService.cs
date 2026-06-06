using CapitalUniversity.Core.Abstractions.CrossCutting.Caching;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Abstractions.Semesters;
using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Application.Semesters.Mappings;
using CapitalUniversity.Core.Domain.Common.Exceptions;
using CapitalUniversity.Core.Domain.Semsters;
using FluentValidation;
using ValidationException = CapitalUniversity.Core.Domain.Common.Exceptions.ValidationException;

namespace CapitalUniversity.Core.Application.Semesters;

public class SemesterService : ISemesterService
{
    private const string SemesterField = "Semester";

    // Semesters are tiny, global reference data (no per-user scope). Every cache
    // entry is keyed by the collection version, so a single bump on any mutation
    // invalidates the object, current and by-year caches at once. Cached payloads
    // are culture-neutral; localization runs on read against fresh copies.
    private const string ObjectKeyPrefix = "semester:object:";
    private const string CurrentKey = "semester:current:";
    private const string ByYearKeyPrefix = "semester:by-year:";
    private const string CollectionVersionKey = "semester:coll:ver";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateSemesterRequest> _createValidator;
    private readonly IValidator<(Guid Id, UpdateSemesterRequest Request)> _updateValidator;
    private readonly ILocalizationService _localization;
    private readonly ICacheService _cache;
    private readonly SemesterMapper _mapper;

    public SemesterService(
        IUnitOfWork unitOfWork,
        IValidator<CreateSemesterRequest> createValidator,
        IValidator<(Guid Id, UpdateSemesterRequest Request)> updateValidator,
        ILocalizationService localization,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _localization = localization;
        _cache = cache;
        _mapper = new SemesterMapper();
    }

    public async Task<SemesterResponse?> GetByIdAsync(Guid id)
    {
        var version = await GetCollectionVersionAsync();
        var dto = await _cache.GetOrSetAsync<SemesterResponse>(
            $"{ObjectKeyPrefix}{id:N}:{version}",
            async _ =>
            {
                var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
                return semester == null ? null : _mapper.MapToResponse(semester);
            },
            CacheTtl,
            CancellationToken.None);
        return dto == null ? null : LocalizeCopy(dto);
    }

    public async Task<SemesterResponse?> GetCurrentAsync()
    {
        var version = await GetCollectionVersionAsync();
        var dto = await _cache.GetOrSetAsync<SemesterResponse>(
            $"{CurrentKey}{version}",
            async _ =>
            {
                var semester = await _unitOfWork.Semesters.GetCurrentAsync();
                return semester == null ? null : _mapper.MapToResponse(semester);
            },
            CacheTtl,
            CancellationToken.None);
        return dto == null ? null : LocalizeCopy(dto);
    }

    public async Task<IEnumerable<SemesterResponse>> GetByAcademicYearIdAsync(Guid academicYearId)
    {
        var version = await GetCollectionVersionAsync();
        var cached = await _cache.GetOrSetAsync<List<SemesterResponse>>(
            $"{ByYearKeyPrefix}{academicYearId:N}:{version}",
            async _ => (await _unitOfWork.Semesters.GetByAcademicYearIdAsync(academicYearId)).Select(_mapper.MapToResponse).ToList(),
            CacheTtl,
            CancellationToken.None);
        return (cached ?? new List<SemesterResponse>()).Select(LocalizeCopy).ToList();
    }

    private async Task<string> GetCollectionVersionAsync()
    {
        var v = await _cache.GetAsync<string>(CollectionVersionKey);
        if (!string.IsNullOrEmpty(v)) return v;
        await _cache.SetAsync(CollectionVersionKey, "0", VersionTtl);
        return "0";
    }

    private Task BumpCollectionVersionAsync() =>
        _cache.SetAsync(CollectionVersionKey, Guid.NewGuid().ToString("N"), VersionTtl);

    /// <summary>Localize onto a NEW response so cached entries are never mutated.</summary>
    private SemesterResponse LocalizeCopy(SemesterResponse s) => new()
    {
        Id = s.Id,
        AcademicYearId = s.AcademicYearId,
        Name = _localization.Get<string>(s.Name),
        Order = s.Order,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        IsCurrent = s.IsCurrent,
        IsClosed = s.IsClosed,
        ClosedAt = s.ClosedAt,
    };

    public async Task<Guid> CreateAsync(CreateSemesterRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var year = await _unitOfWork.AcademicYears.GetByIdAsync(request.AcademicYearId);
        if (year == null)
        {
            throw new ValidationException("AcademicYearId", LocalizedKeys.Semesters.AcademicYearMissing);
        }

        if (request.StartDate < year.StartDate || request.EndDate > year.EndDate)
        {
            throw new ValidationException(SemesterField, LocalizedKeys.Semesters.DatesOutsideAcademicYear);
        }

        if (await _unitOfWork.Semesters.HasOverlapAsync(request.AcademicYearId, request.StartDate, request.EndDate))
        {
            throw new ValidationException(SemesterField, LocalizedKeys.Semesters.DatesOverlap);
        }

        var semester = _mapper.MapToEntity(request);
        semester.IsCurrent = IsDateInRange(DateTime.UtcNow, semester.StartDate, semester.EndDate);

        if (semester.IsCurrent)
        {
            // H7 — deactivate-then-activate in two flushes when (and only when)
            // there is an existing current row to clear, so the filtered
            // UNIQUE index never sees two rows with IsCurrent = 1 in flight.
            if (await DeactivateCurrentSemesterAsync())
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }

        await _unitOfWork.Semesters.AddAsync(semester);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
        return semester.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateSemesterRequest request)
    {
        var validationResult = await _updateValidator.ValidateAsync((id, request));
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);
        semester.EnsureMutable();

        var year = await _unitOfWork.AcademicYears.GetByIdAsync(semester.AcademicYearId);
        if (year == null) throw new NotFoundException(LocalizedKeys.Semesters.AcademicYearNotFound);

        var startDate = request.StartDate ?? semester.StartDate;
        var endDate = request.EndDate ?? semester.EndDate;

        if (endDate <= startDate)
        {
            throw new ValidationException("EndDate", LocalizedKeys.Semesters.EndAfterStart);
        }

        if (startDate < year.StartDate || endDate > year.EndDate)
        {
            throw new ValidationException(SemesterField, LocalizedKeys.Semesters.DatesOutsideAcademicYear);
        }

        if (await _unitOfWork.Semesters.HasOverlapAsync(semester.AcademicYearId, startDate, endDate, id))
        {
            throw new ValidationException("Semester", LocalizedKeys.Semesters.DatesOverlap);
        }

        if (request.Name != null) semester.Name = LocalizedJson.Normalize(request.Name);
        _mapper.UpdateEntity(request, semester);

        semester.IsCurrent = IsDateInRange(DateTime.UtcNow, semester.StartDate, semester.EndDate);
        semester.UpdatedAt = DateTime.UtcNow;

        if (semester.IsCurrent)
        {
            // H7 — see CreateAsync.
            if (await DeactivateCurrentSemesterAsync(id))
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }

        _unitOfWork.Semesters.Update(semester);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);
        semester.EnsureMutable();

        _unitOfWork.Semesters.Delete(semester);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task CloseRecordAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);

        semester.Close();
        _unitOfWork.Semesters.Update(semester);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task OpenRecordAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);

        semester.Reopen();
        _unitOfWork.Semesters.Update(semester);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }


    public async Task ResolveCurrentSemesterAsync()
    {
        // H7 — two-phase update against the new filtered UNIQUE index on
        // (AcademicYearId, IsCurrent) WHERE IsCurrent = 1. See the matching
        // comment on AcademicYearService.ResolveCurrentYearAsync.
        var now = DateTime.UtcNow;
        var currentYear = await _unitOfWork.AcademicYears.GetCurrentAsync();
        if (currentYear == null)
        {
            await DeactivateCurrentSemesterAsync();
            await _unitOfWork.SaveChangesAsync();
            await BumpCollectionVersionAsync();
            return;
        }

        var semesters = await _unitOfWork.Semesters.GetByAcademicYearIdAsync(currentYear.Id);
        var currentSemester = semesters.FirstOrDefault(x => IsDateInRange(now, x.StartDate, x.EndDate));

        var dirty = false;
        foreach (var semester in semesters)
        {
            var shouldBeCurrent = currentSemester != null && semester.Id == currentSemester.Id;
            if (semester.IsCurrent && !shouldBeCurrent)
            {
                semester.IsCurrent = false;
                semester.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Semesters.Update(semester);
                dirty = true;
            }
        }
        if (dirty)
        {
            await _unitOfWork.SaveChangesAsync();
            await BumpCollectionVersionAsync();
        }

        if (currentSemester is null) return;
        if (currentSemester.IsCurrent) return;

        currentSemester.IsCurrent = true;
        currentSemester.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Semesters.Update(currentSemester);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    private async Task<bool> DeactivateCurrentSemesterAsync(Guid? excludeId = null)
    {
        var current = await _unitOfWork.Semesters.GetCurrentAsync();
        if (current != null && current.Id != excludeId)
        {
            current.IsCurrent = false;
            current.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Semesters.Update(current);
            return true;
        }
        return false;
    }

    private static bool IsDateInRange(DateTime date, DateTime start, DateTime end) =>
        date >= start && date <= end;
}
