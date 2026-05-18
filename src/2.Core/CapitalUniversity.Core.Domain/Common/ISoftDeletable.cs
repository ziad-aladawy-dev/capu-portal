namespace CapitalUniversity.Core.Domain.Common;

/// <summary>
/// Marker interface for entities whose <c>IsDeleted</c> flag participates in a
/// global query filter. Per <c>docs/RemediationPlan.md</c> P0.6, the filter is
/// applied ONLY to Invoice, PaymentTransaction, and StudentProfileRecord — all
/// other entities remain global even though <see cref="BaseEntity.IsDeleted"/>
/// exists for legacy reasons.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
