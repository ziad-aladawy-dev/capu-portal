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
    // IInvoiceRepository + IStudentProfileRecordRepository removed — those
    // contracts now live alongside their implementations in
    // Module.Payments / Module.Student. Module services inject the
    // repositories directly; Core.Abstractions has no module dependency.
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
