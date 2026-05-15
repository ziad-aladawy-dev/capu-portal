using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authentication;
using CapitalUniversity.Core.Domain.Identity;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CapitalUniversity.Core.Infrastructure.Services.Authentication;

public class UserCredentialResolver : IUserCredentialResolver
{
    private readonly CoreDbContext _dbContext;

    public UserCredentialResolver(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IUserCredential> ResolveCredentialAsync(string identifier, CancellationToken cancellationToken = default)
    {
        var student = await _dbContext.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.NationalId == identifier , cancellationToken);

        if (student != null)
        {
            return new StudentUserCredential(student);
        }

        var staff = await _dbContext.Staffs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.NationalId == identifier, cancellationToken);

        if (staff != null)
        {
            return new StaffUserCredential(staff);
        }

        return null!;
    }
}

public class StudentUserCredential : IUserCredential
{
    private readonly Student _student;

    public StudentUserCredential(Student student)
    {
        _student = student;
    }

    public Guid Id => _student.Id;
    public string Identifier => _student.NationalId;
    public string PasswordHash => _student.PasswordHash ?? string.Empty;
    public DateTime? PasswordExpiry => _student.PasswordExpiry ?? _student.CreatedAt.AddDays(365);
    public string Role => "Student";
    public string Name => _student.Name;
    public string Email => _student.Email;
    public Guid? StructureNodeId => _student.StructureNodeId;
}

public class StaffUserCredential : IUserCredential
{
    private readonly Staff _staff;

    public StaffUserCredential(Staff staff)
    {
        _staff = staff;
    }

    public Guid Id => _staff.Id;
    public string Identifier => _staff.NationalId;
    public string PasswordHash => _staff.PasswordHash ?? string.Empty;
    public DateTime? PasswordExpiry => null;
    public string Role => "Staff";
    public string Name => _staff.Name;
    public string Email => _staff.Email ?? string.Empty;
    public Guid? StructureNodeId => null;
}
