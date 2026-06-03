using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.UniTests._Helpers;

/// <summary>
/// Test <see cref="IUnitOfWork"/> that forwards <see cref="SaveChangesAsync"/> to a
/// real (InMemory) <see cref="CoreDbContext"/>, so command handlers exercised in unit
/// tests persist through the same context the test asserts against. The aggregate
/// repository properties are unused by the Role handlers and throw if touched.
/// <see cref="ExecuteInSerializableTransactionAsync"/> runs the action directly,
/// mirroring the InMemory-provider no-op behaviour of the production unit of work.
/// </summary>
internal sealed class TestUnitOfWork : IUnitOfWork
{
    private readonly CoreDbContext _context;

    public TestUnitOfWork(CoreDbContext context) => _context = context;

    public IStudentRepository Students => throw new NotSupportedException();
    public IStaffRepository Staff => throw new NotSupportedException();
    public IStructureNodeRepository StructureNodes => throw new NotSupportedException();
    public IAcademicYearRepository AcademicYears => throw new NotSupportedException();
    public ISemesterRepository Semesters => throw new NotSupportedException();
    public ICourseRepository Courses => throw new NotSupportedException();
    public IAcademicPlanRepository AcademicPlans => throw new NotSupportedException();

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public Task ExecuteInSerializableTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) => action(cancellationToken);

    public void Dispose() { }
}
