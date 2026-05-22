using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Domain.Semsters;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Semesters.Mappings;

// RequiredMappingStrategy.None: audit base props are intentionally absent
// from the DTOs.
// AllowNullPropertyAssignment = false makes UpdateEntity PATCH-style — null
// source fields skip assignment rather than nulling out the entity. Both
// settings cooperate to silence RMG012/020/089 without contract changes.
[Mapper(
    RequiredMappingStrategy = RequiredMappingStrategy.None,
    AllowNullPropertyAssignment = false)]
public partial class AcademicYearMapper
{
    public partial AcademicYearResponse MapToResponse(AcademicYear entity);

    public partial AcademicYear MapToEntity(CreateAcademicYearRequest request);

    public partial void UpdateEntity(UpdateAcademicYearRequest request, AcademicYear entity);
}
