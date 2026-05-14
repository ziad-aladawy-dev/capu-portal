using CapitalUniversity.Core.Domain.Repositories;

namespace CapitalUniversity.Core.Domain.Repositories;

public interface IUnitOfWork : IDisposable
{
    IStudentRepository Students { get; }
    IStaffRepository Staff { get; }
    IStructureNodeRepository StructureNodes { get; }
    IAcademicYearRepository AcademicYears { get; }
    ISemesterRepository Semesters { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

