using CapitalUniversity.Modules.StudentServices.Domain;

namespace CapitalUniversity.Modules.StudentServices.Repositories;

public interface IStudentServiceRepository
{
    /// <summary>Loads a service with its fields + documents eagerly. Returns null for missing or soft-deleted rows.</summary>
    Task<StudentService?> GetByIdAsync(Guid id, bool includeChildren = true, CancellationToken cancellationToken = default);

    Task<StudentService?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Paged list with optional search/active filter. Returned tuple is (items, total) for paging metadata.</summary>
    Task<(IReadOnlyList<StudentService> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        string? search,
        bool? isActive,
        CancellationToken cancellationToken = default);

    /// <summary>Active services only — student-facing catalog query.</summary>
    Task<IReadOnlyList<StudentService>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId, CancellationToken cancellationToken = default);

    Task AddAsync(StudentService service, CancellationToken cancellationToken = default);
    void Update(StudentService service);
    void Delete(StudentService service);
}
