using CapitalUniversity.Core.Abstractions.Repositories;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly CoreDbContext _context;

    public UnitOfWork(
        CoreDbContext context,
        IStudentRepository students,
        IStaffRepository staff,
        IStructureNodeRepository structureNodes,
        IAcademicYearRepository academicYears,
        ISemesterRepository semesters,
        ICourseRepository courses,
        IAcademicPlanRepository academicPlans,
        IStudentProfileRecordRepository studentProfileRecords)
    {
        _context = context;
        Students = students;
        Staff = staff;
        StructureNodes = structureNodes;
        AcademicYears = academicYears;
        Semesters = semesters;
        Courses = courses;
        AcademicPlans = academicPlans;
        StudentProfileRecords = studentProfileRecords;
    }

    public IStudentRepository Students { get; }
    public IStaffRepository Staff { get; }
    public IStructureNodeRepository StructureNodes { get; }
    public IAcademicYearRepository AcademicYears { get; }
    public ISemesterRepository Semesters { get; }
    public ICourseRepository Courses { get; }
    public IAcademicPlanRepository AcademicPlans { get; }
    public IStudentProfileRecordRepository StudentProfileRecords { get; }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

