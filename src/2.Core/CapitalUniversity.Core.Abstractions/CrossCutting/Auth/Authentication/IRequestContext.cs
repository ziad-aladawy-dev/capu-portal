using System;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;

public interface IRequestContext
{
    Guid? UserId { get; }
    string Role { get; }
    Guid? ActiveFacultyId { get; }
    Guid? ActiveProgramId { get; }
    Guid? ActiveAcademicYearId { get; }
    Guid? ActiveSemesterId { get; }
}
