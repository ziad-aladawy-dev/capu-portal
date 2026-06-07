using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury.DTOs;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using CapitalUniversity.Modules.Payments.Repositories.Treasury;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>Read-side for student fees, scope-enforced per the platform convention.</summary>
public sealed class StudentFeeQueryService : IStudentFeeQueryService
{
    private readonly IStudentFeeRepository _fees;
    private readonly IEffectiveScope _scope;

    public StudentFeeQueryService(IStudentFeeRepository fees, IEffectiveScope scope)
    {
        _fees = fees;
        _scope = scope;
    }

    public async Task<IReadOnlyList<StudentFeeResponse>> GetUnpaidForStudentAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!await _scope.CanAccessStudentAsync(studentId, cancellationToken))
        {
            return Array.Empty<StudentFeeResponse>();
        }
        var fees = await _fees.GetPendingForStudentAsync(studentId, cancellationToken);
        return fees.Select(ToResponse).ToList();
    }

    public async Task<StudentFeeResponse?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken = default)
    {
        var fee = await _fees.GetByIdAsync(feeId, cancellationToken);
        if (fee is null) return null;
        if (!await _scope.CanAccessStudentAsync(fee.StudentId, cancellationToken)) return null;
        return ToResponse(fee);
    }

    private static StudentFeeResponse ToResponse(StudentFee f) => new()
    {
        Id = f.Id,
        StudentId = f.StudentId,
        ReceiptId = f.ReceiptId,
        Quantity = f.Quantity,
        UnitAmount = f.UnitAmount,
        TotalAmount = f.TotalAmount,
        Currency = f.Currency,
        Status = f.Status,
        SourceModule = f.SourceModule,
        SourceReferenceId = f.SourceReferenceId,
        OrderId = f.OrderId,
        CreatedAt = f.CreatedAt,
    };
}
