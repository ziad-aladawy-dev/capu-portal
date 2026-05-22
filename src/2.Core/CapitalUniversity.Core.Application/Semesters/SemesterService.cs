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

    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateSemesterRequest> _createValidator;
    private readonly IValidator<(Guid Id, UpdateSemesterRequest Request)> _updateValidator;
    private readonly ILocalizationService _localization;
    private readonly SemesterMapper _mapper;

    public SemesterService(
        IUnitOfWork unitOfWork,
        IValidator<CreateSemesterRequest> createValidator,
        IValidator<(Guid Id, UpdateSemesterRequest Request)> updateValidator,
        ILocalizationService localization)
    {
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _localization = localization;
        _mapper = new SemesterMapper();
    }

    public async Task<SemesterResponse?> GetByIdAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        return semester == null ? null : Localize(_mapper.MapToResponse(semester));
    }

    public async Task<SemesterResponse?> GetCurrentAsync()
    {
        var semester = await _unitOfWork.Semesters.GetCurrentAsync();
        return semester == null ? null : Localize(_mapper.MapToResponse(semester));
    }

    public async Task<IEnumerable<SemesterResponse>> GetByAcademicYearIdAsync(Guid academicYearId)
    {
        var semesters = await _unitOfWork.Semesters.GetByAcademicYearIdAsync(academicYearId);
        return semesters.Select(s => Localize(_mapper.MapToResponse(s)));
    }

    /// <summary>
    /// Decode the bilingual fields on a <see cref="SemesterResponse"/> against
    /// the current culture. <c>Semester.Name</c> is the only user-visible
    /// string on the response that may carry the canonical
    /// <c>{"ar":"…","en":"…"}</c> JSON shape; legacy plain text passes
    /// through untouched.
    /// </summary>
    private SemesterResponse Localize(SemesterResponse response)
    {
        response.Name = _localization.Get<string>(response.Name);
        return response;
    }

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
            await DeactivateCurrentSemesterAsync();
        }

        await _unitOfWork.Semesters.AddAsync(semester);
        await _unitOfWork.SaveChangesAsync();
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
            await DeactivateCurrentSemesterAsync(id);
        }

        _unitOfWork.Semesters.Update(semester);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);
        semester.EnsureMutable();

        _unitOfWork.Semesters.Delete(semester);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CloseRecordAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);

        semester.Close();
        _unitOfWork.Semesters.Update(semester);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task OpenRecordAsync(Guid id)
    {
        var semester = await _unitOfWork.Semesters.GetByIdAsync(id);
        if (semester == null) throw new NotFoundException(LocalizedKeys.Semesters.NotFound);

        semester.Reopen();
        _unitOfWork.Semesters.Update(semester);
        await _unitOfWork.SaveChangesAsync();
    }


    public async Task ResolveCurrentSemesterAsync()
    {
        var now = DateTime.UtcNow;
        var currentYear = await _unitOfWork.AcademicYears.GetCurrentAsync();
        if (currentYear == null)
        {
            await DeactivateCurrentSemesterAsync();
            await _unitOfWork.SaveChangesAsync();
            return;
        }

        var semesters = await _unitOfWork.Semesters.GetByAcademicYearIdAsync(currentYear.Id);
        var currentSemester = semesters.FirstOrDefault(x => IsDateInRange(now, x.StartDate, x.EndDate));

        foreach (var semester in semesters)
        {
            bool shouldBeCurrent = (currentSemester != null && semester.Id == currentSemester.Id);
            if (semester.IsCurrent != shouldBeCurrent)
            {
                semester.IsCurrent = shouldBeCurrent;
                semester.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Semesters.Update(semester);
            }
        }
        
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task DeactivateCurrentSemesterAsync(Guid? excludeId = null)
    {
        var current = await _unitOfWork.Semesters.GetCurrentAsync();
        if (current != null && current.Id != excludeId)
        {
            current.IsCurrent = false;
            current.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Semesters.Update(current);
        }
    }

    private static bool IsDateInRange(DateTime date, DateTime start, DateTime end) =>
        date >= start && date <= end;
}
