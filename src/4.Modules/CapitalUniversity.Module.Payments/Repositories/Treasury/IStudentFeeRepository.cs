using CapitalUniversity.Modules.Payments.Domain.Treasury;

namespace CapitalUniversity.Modules.Payments.Repositories.Treasury;

public interface IStudentFeeRepository
{
    Task<StudentFee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentFee>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentFee>> GetPendingForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task AddAsync(StudentFee fee, CancellationToken cancellationToken = default);
    void Update(StudentFee fee);
    void ResetChangeTracker();
}
