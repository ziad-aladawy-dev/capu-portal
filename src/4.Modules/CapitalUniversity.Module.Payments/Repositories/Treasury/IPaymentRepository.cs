using CapitalUniversity.Modules.Payments.Domain.Treasury;

namespace CapitalUniversity.Modules.Payments.Repositories.Treasury;

public interface IPaymentRepository
{
    Task<Payment?> GetByFeeIdAsync(Guid feeId, CancellationToken cancellationToken = default);
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
}
