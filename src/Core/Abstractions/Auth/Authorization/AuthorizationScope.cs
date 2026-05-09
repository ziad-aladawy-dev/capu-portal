using System;

namespace CapitalUniversity.Core.Abstractions.Auth.Authorization;

public class AuthorizationScope
{
    public required string Domain { get; set; }
    public required string Year { get; set; }
    public required string Semester { get; set; }

    public Guid? UniversityId { get; set; }
    public Guid? FacultyId { get; set; }
    public Guid? ProgramId { get; set; }
}
