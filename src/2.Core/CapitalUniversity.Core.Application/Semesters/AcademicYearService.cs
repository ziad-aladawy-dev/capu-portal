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

public class AcademicYearService : IAcademicYearService
{
    // Academic years are tiny, global reference data (no per-user scope). Every
    // cache entry is keyed by the collection version, so a single bump on any
    // mutation invalidates the object, current and list caches at once — no
    // per-key removal needed. Cached payloads are culture-neutral; localization
    // runs on read against fresh copies. See docs/caching-strategy.md.
    private const string ObjectKeyPrefix = "academicyear:object:";
    private const string CurrentKey = "academicyear:current:";
    private const string AllKey = "academicyear:all:";
    private const string CollectionVersionKey = "academicyear:coll:ver";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateAcademicYearRequest> _createValidator;
    private readonly IValidator<(Guid Id, UpdateAcademicYearRequest Request)> _updateValidator;
    private readonly ILocalizationService _localization;
    private readonly ICacheService _cache;
    private readonly AcademicYearMapper _mapper;

    public AcademicYearService(
        IUnitOfWork unitOfWork,
        IValidator<CreateAcademicYearRequest> createValidator,
        IValidator<(Guid Id, UpdateAcademicYearRequest Request)> updateValidator,
        ILocalizationService localization,
        ICacheService cache)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _localization = localization;
        _cache = cache;
        _mapper = new AcademicYearMapper();
    }

    public async Task<AcademicYearResponse?> GetByIdAsync(Guid id)
    {
        var version = await GetCollectionVersionAsync();
        var dto = await _cache.GetOrSetAsync<AcademicYearResponse>(
            $"{ObjectKeyPrefix}{id:N}:{version}",
            async _ =>
            {
                var year = await _unitOfWork.AcademicYears.GetByIdAsync(id);
                return year == null ? null : _mapper.MapToResponse(year);
            },
            CacheTtl,
            CancellationToken.None);
        return dto == null ? null : LocalizeCopy(dto);
    }

    public async Task<AcademicYearResponse?> GetCurrentAsync()
    {
        var version = await GetCollectionVersionAsync();
        var dto = await _cache.GetOrSetAsync<AcademicYearResponse>(
            $"{CurrentKey}{version}",
            async _ =>
            {
                var year = await _unitOfWork.AcademicYears.GetCurrentAsync();
                return year == null ? null : _mapper.MapToResponse(year);
            },
            CacheTtl,
            CancellationToken.None);
        return dto == null ? null : LocalizeCopy(dto);
    }

    public async Task<IEnumerable<AcademicYearResponse>> GetAllAsync()
    {
        var version = await GetCollectionVersionAsync();
        var cached = await _cache.GetOrSetAsync<List<AcademicYearResponse>>(
            $"{AllKey}{version}",
            async _ => (await _unitOfWork.AcademicYears.GetAllAsync()).Select(_mapper.MapToResponse).ToList(),
            CacheTtl,
            CancellationToken.None);
        return (cached ?? new List<AcademicYearResponse>()).Select(LocalizeCopy).ToList();
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
    private AcademicYearResponse LocalizeCopy(AcademicYearResponse s) => new()
    {
        Id = s.Id,
        Name = _localization.Get<string>(s.Name),
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        IsCurrent = s.IsCurrent,
        IsClosed = s.IsClosed,
        ClosedAt = s.ClosedAt,
    };

    public async Task<Guid> CreateAsync(CreateAcademicYearRequest request)
    {
        var validationResult = await _createValidator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        if (await _unitOfWork.AcademicYears.HasOverlapAsync(request.StartDate, request.EndDate))
        {
            throw new ValidationException("AcademicYear", LocalizedKeys.Semesters.YearDatesOverlap);
        }

        var year = _mapper.MapToEntity(request);
        year.IsCurrent = IsDateInRange(DateTime.UtcNow, year.StartDate, year.EndDate);

        if (year.IsCurrent)
        {
            // H7 — deactivate-then-activate in two flushes when (and only when)
            // there is an existing current row to clear, so the filtered UNIQUE
            // index never sees two rows with IsCurrent = 1 in flight. When no
            // current row exists yet, the second flush below is the only one.
            if (await DeactivateCurrentYearAsync())
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }

        await _unitOfWork.AcademicYears.AddAsync(year);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
        return year.Id;
    }

    public async Task UpdateAsync(Guid id, UpdateAcademicYearRequest request)
    {
        var validationResult = await _updateValidator.ValidateAsync((id, request));
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        var year = await _unitOfWork.AcademicYears.GetByIdAsync(id);
        if (year == null) throw new NotFoundException(LocalizedKeys.Semesters.AcademicYearNotFound);
        year.EnsureMutable();

        var startDate = request.StartDate ?? year.StartDate;
        var endDate = request.EndDate ?? year.EndDate;

        if (endDate <= startDate)
        {
            throw new ValidationException("EndDate", LocalizedKeys.Semesters.EndAfterStart);
        }

        if (await _unitOfWork.AcademicYears.HasOverlapAsync(startDate, endDate, id))
        {
            throw new ValidationException("AcademicYear", LocalizedKeys.Semesters.YearDatesOverlap);
        }

        if (request.Name != null) year.Name = LocalizedJson.Normalize(request.Name);
        _mapper.UpdateEntity(request, year);
        year.IsCurrent = IsDateInRange(DateTime.UtcNow, year.StartDate, year.EndDate);
        year.UpdatedAt = DateTime.UtcNow;

        if (year.IsCurrent)
        {
            // H7 — see CreateAsync. Flush only when there is a different row
            // to clear; otherwise the single SaveChanges below is enough.
            if (await DeactivateCurrentYearAsync(id))
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }

        _unitOfWork.AcademicYears.Update(year);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var year = await _unitOfWork.AcademicYears.GetByIdAsync(id);
        if (year == null) throw new NotFoundException(LocalizedKeys.Semesters.AcademicYearNotFound);
        year.EnsureMutable();

        _unitOfWork.AcademicYears.Delete(year);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task CloseRecordAsync(Guid id)
    {
        var year = await _unitOfWork.AcademicYears.GetByIdAsync(id);
        if (year == null) throw new NotFoundException(LocalizedKeys.Semesters.AcademicYearNotFound);

        year.Close();
        _unitOfWork.AcademicYears.Update(year);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task OpenRecordAsync(Guid id)
    {
        var year = await _unitOfWork.AcademicYears.GetByIdAsync(id);
        if (year == null) throw new NotFoundException(LocalizedKeys.Semesters.AcademicYearNotFound);

        year.Reopen();
        _unitOfWork.AcademicYears.Update(year);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    public async Task ResolveCurrentYearAsync()
    {
        // H7 — two-phase update so the filtered UNIQUE index on (IsCurrent
        // WHERE IsCurrent = 1) never sees two true rows in the same SaveChanges
        // batch. Pass 1: deactivate any row that should no longer be current.
        // Flush. Pass 2: activate the row that should be current. Flush.
        var now = DateTime.UtcNow;
        var years = await _unitOfWork.AcademicYears.GetAllAsync();
        var currentYear = years.FirstOrDefault(x => IsDateInRange(now, x.StartDate, x.EndDate));

        var dirty = false;
        foreach (var year in years)
        {
            var shouldBeCurrent = currentYear != null && year.Id == currentYear.Id;
            if (year.IsCurrent && !shouldBeCurrent)
            {
                year.IsCurrent = false;
                year.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.AcademicYears.Update(year);
                dirty = true;
            }
        }
        if (dirty)
        {
            await _unitOfWork.SaveChangesAsync();
            await BumpCollectionVersionAsync();
        }

        if (currentYear is null) return;
        if (currentYear.IsCurrent) return;

        currentYear.IsCurrent = true;
        currentYear.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.AcademicYears.Update(currentYear);
        await _unitOfWork.SaveChangesAsync();
        await BumpCollectionVersionAsync();
    }

    private async Task<bool> DeactivateCurrentYearAsync(Guid? excludeId = null)
    {
        var current = await _unitOfWork.AcademicYears.GetCurrentAsync();
        if (current != null && current.Id != excludeId)
        {
            current.IsCurrent = false;
            current.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.AcademicYears.Update(current);
            return true;
        }
        return false;
    }

    private static bool IsDateInRange(DateTime date, DateTime start, DateTime end) =>
        date >= start && date <= end;
}
