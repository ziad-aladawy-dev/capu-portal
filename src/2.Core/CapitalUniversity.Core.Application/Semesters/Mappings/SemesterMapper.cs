using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Domain.Semsters;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Semesters.Mappings;

// RequiredMappingStrategy.None: audit base props + AcademicYearId
// immutability after create are deliberate omissions from the DTOs.
// AllowNullPropertyAssignment = false: UpdateEntity is PATCH-style, null
// source fields don't overwrite the entity.
[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.None,
    AllowNullPropertyAssignment = false)]
public partial class SemesterMapper
{
    public partial SemesterResponse MapToResponse(Semester entity);

    public partial Semester MapToEntity(CreateSemesterRequest request);

    public partial void UpdateEntity(UpdateSemesterRequest request, Semester entity);
}
