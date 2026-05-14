using CapitalUniversity.Core.Abstractions.Semesters.DTOs;
using CapitalUniversity.Core.Domain.Semsters;
using Riok.Mapperly.Abstractions;

namespace CapitalUniversity.Core.Application.Semesters.Mappings;

[Mapper]
public partial class SemesterMapper
{
    public partial SemesterResponse MapToResponse(Semester entity);

    public partial Semester MapToEntity(CreateSemesterRequest request);

    public partial void UpdateEntity(UpdateSemesterRequest request, Semester entity);
}
