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

    /// <summary>
    /// M1 — run a write-side critical section under a SERIALIZABLE transaction
    /// on the underlying relational connection, so range-locks block other
    /// writers between a read predicate and the matching insert. Used by
    /// the schedule module's overlap-then-insert flow. On non-relational
    /// providers (InMemory tests) this is a no-op and the action runs
    /// directly — those providers cannot race in any case.
    /// </summary>
    Task ExecuteInSerializableTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}
