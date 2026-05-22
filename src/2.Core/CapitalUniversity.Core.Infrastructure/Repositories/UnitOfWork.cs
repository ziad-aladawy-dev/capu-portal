using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

/// <summary>
/// Bundles the repositories surfaced by <see cref="IUnitOfWork"/>. Lets
/// <see cref="UnitOfWork"/> stay under the 7-parameter constructor limit
/// without losing the explicit per-repository property surface used by
/// callers.
/// </summary>
public sealed record UnitOfWorkRepositories(
    IStudentRepository Students,
    IStaffRepository Staff,
    IStructureNodeRepository StructureNodes,
    IAcademicYearRepository AcademicYears,
    ISemesterRepository Semesters,
    ICourseRepository Courses,
    IAcademicPlanRepository AcademicPlans);

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CoreDbContext _context;

    public UnitOfWork(CoreDbContext context, UnitOfWorkRepositories repositories)
    {
        _context = context;
        Students = repositories.Students;
        Staff = repositories.Staff;
        StructureNodes = repositories.StructureNodes;
        AcademicYears = repositories.AcademicYears;
        Semesters = repositories.Semesters;
        Courses = repositories.Courses;
        AcademicPlans = repositories.AcademicPlans;
    }

    public IStudentRepository Students { get; }
    public IStaffRepository Staff { get; }
    public IStructureNodeRepository StructureNodes { get; }
    public IAcademicYearRepository AcademicYears { get; }
    public ISemesterRepository Semesters { get; }
    public ICourseRepository Courses { get; }
    public IAcademicPlanRepository AcademicPlans { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    // CoreDbContext + repositories are scoped DI services; the DI container
    // already disposes them at scope end. UnitOfWork itself owns no
    // disposable resources — SuppressFinalize keeps CA1816 quiet and the
    // sealed type means no derived class can introduce a finalizer.
    public void Dispose() => GC.SuppressFinalize(this);
}
