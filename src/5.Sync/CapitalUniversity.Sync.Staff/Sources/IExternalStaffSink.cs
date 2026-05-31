using CapitalUniversity.Sync.Staff.Domain;

namespace CapitalUniversity.Sync.Staff.Sources;

/// <summary>
/// Push counterpart to <see cref="IExternalStaffSource"/>. Identical contract shape
/// to the Students module's sink so operational patterns transfer 1:1.
/// </summary>
public interface IExternalStaffSink
{
    Task PushAsync(ExternalStaff payload, CancellationToken cancellationToken);
}