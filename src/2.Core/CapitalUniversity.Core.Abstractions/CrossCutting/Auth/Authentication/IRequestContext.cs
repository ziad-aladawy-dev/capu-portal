using System;

namespace CapitalUniversity.Core.Abstractions.Cross-Cutting.Auth.Authentication;

public interface IRequestContext
{
    Guid? UserId { get; }
    string Role { get; }
    Guid? ActiveFacultyId { get; }
    Guid? ActiveProgramId { get; }
    Guid? ActiveAcademicYearId { get; }
    Guid? ActiveSemesterId { get; }
}
