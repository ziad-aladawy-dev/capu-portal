namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IUnitOfWork : IDisposable
{
    IStudentRepository Students { get; }
    IStaffRepository Staff { get; }
    IStructureNodeRepository StructureNodes { get; }
    IAcademicYearRepository AcademicYears { get; }
    ISemesterRepository Semesters { get; }
    ICourseRepository Courses { get; }
    IAcademicPlanRepository AcademicPlans { get; }
    IInvoiceRepository Invoices { get; }
    IStudentProfileRecordRepository StudentProfileRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

