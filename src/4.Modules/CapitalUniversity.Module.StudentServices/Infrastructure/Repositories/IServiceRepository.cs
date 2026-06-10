using CapitalUniversity.Module.StudentServices.Domain;

namespace CapitalUniversity.Module.StudentServices.Infrastructure.Repositories;

public interface IServiceRepository
{
    Task<Service?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Service?> GetByIdWithWorkflowAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Service?> GetByIdWithScopeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<Service>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task<List<Service>> GetAvailableForStudentAsync(
        Guid studentId,
        string? studentNodePath,
        int? studentLevelNumber,
        Guid? currentAcademicYearId,
        Guid? currentSemesterId,
        CancellationToken cancellationToken = default);
    Task AddAsync(Service service, CancellationToken cancellationToken = default);
    void Update(Service service);
    Task UpdateServiceScopeNodesAsync(Guid serviceId, IEnumerable<Guid> newScopeNodeIds, CancellationToken cancellationToken = default);
    void Delete(Service service);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> IsServiceInUseAsync(Guid serviceId, CancellationToken cancellationToken = default);
    Task<bool> IsServiceInUseByWorkflowAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}