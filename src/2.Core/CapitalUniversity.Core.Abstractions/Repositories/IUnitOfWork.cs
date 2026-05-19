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
    // Invoices repository removed — IInvoiceRepository now lives in
    // Module.Payments.Abstractions; Core.Abstractions has no module dependency.
    // Payments services inject IInvoiceRepository directly.
    IStudentProfileRecordRepository StudentProfileRecords { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

