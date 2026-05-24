using CapitalUniversity.Modules.StudentServices.Abstractions;
using CapitalUniversity.Modules.StudentServices.Abstractions.DTOs;
using CapitalUniversity.Modules.StudentServices.Domain;

namespace CapitalUniversity.Modules.StudentServices.Repositories;

public interface IStudentServiceRequestRepository
{
    Task<StudentServiceRequest?> GetByIdAsync(Guid id, bool includeChildren = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Find every request whose <c>PaymentReferenceId</c> matches the supplied
    /// invoice id. Returns an empty list (not null) when no requests are tied
    /// to that invoice — used by the <c>InvoicePaidEventHandler</c> to advance
    /// the request status automatically when the Payments module signals
    /// settlement. Usually one request per invoice (SubmitAsync issues a
    /// fresh invoice per request), but the contract is plural so an upstream
    /// merge policy change does not silently miss requests.
    /// </summary>
    Task<IReadOnlyList<StudentServiceRequest>> GetByPaymentReferenceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>List query supporting filter / search / sort / paging — used by both student and staff list endpoints.</summary>
    Task<(IReadOnlyList<StudentServiceRequest> Items, int Total)> ListAsync(
        StudentServiceRequestListQuery query,
        IReadOnlyCollection<ServiceRequestStatus>? statusInclude,
        CancellationToken cancellationToken = default);

    Task AddAsync(StudentServiceRequest request, CancellationToken cancellationToken = default);
    void Update(StudentServiceRequest request);
    void Delete(StudentServiceRequest request);
}
