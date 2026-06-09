using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;

namespace CapitalUniversity.Modules.Payments.Abstractions.Treasury;

/// <summary>Read-side for student fees (selection UI / order assembly).</summary>
public interface IStudentFeeQueryService
{
    Task<IReadOnlyList<StudentFeeResponse>> GetUnpaidForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<StudentFeeResponse?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken = default);
}
