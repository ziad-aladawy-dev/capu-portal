using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Domain.Semsters;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Semesters.Mappings;

[Mapper]
public partial class AcademicYearMapper
{
    public partial AcademicYearResponse MapToResponse(AcademicYear entity);
    
    public partial AcademicYear MapToEntity(CreateAcademicYearRequest request);

    public partial void UpdateEntity(UpdateAcademicYearRequest request, AcademicYear entity);
}
