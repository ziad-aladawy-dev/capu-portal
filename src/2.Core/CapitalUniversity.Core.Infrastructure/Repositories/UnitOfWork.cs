using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CoreDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public UnitOfWork(
        CoreDbContext context,
        IStudentRepository students,
        IStaffRepository staff,
        IStructureNodeRepository structureNodes,
        IAcademicYearRepository academicYears,
        ISemesterRepository semesters,
        ICourseRepository courses,
        IAcademicPlanRepository academicPlans)
    {
        _context = context;
        Students = students;
        Staff = staff;
        StructureNodes = structureNodes;
        AcademicYears = academicYears;
        Semesters = semesters;
        Courses = courses;
        AcademicPlans = academicPlans;
    }

    public IStudentRepository Students { get; }
    public IStaffRepository Staff { get; }
    public IStructureNodeRepository StructureNodes { get; }
    public IAcademicYearRepository AcademicYears { get; }
    public ISemesterRepository Semesters { get; }
    public ICourseRepository Courses { get; }
    public IAcademicPlanRepository AcademicPlans { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            return;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            if (_currentTransaction != null)
            {
                _currentTransaction.Dispose();
                _currentTransaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
            _currentTransaction.Dispose();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
    }
}

