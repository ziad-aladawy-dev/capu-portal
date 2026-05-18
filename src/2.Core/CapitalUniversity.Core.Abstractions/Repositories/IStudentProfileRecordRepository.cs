using CapitalUniversity.Core.Domain.StudentInformation;

namespace CapitalUniversity.Core.Abstractions.Repositories;

public interface IStudentProfileRecordRepository
{
    Task<StudentProfileRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentProfileRecord>> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
    Task<StudentProfileRecord?> GetForStudentCategoryAsync(Guid studentId, StudentProfileCategory category, string customCategoryKey, CancellationToken cancellationToken = default);
    Task AddAsync(StudentProfileRecord record, CancellationToken cancellationToken = default);
    void Update(StudentProfileRecord record);
    void Delete(StudentProfileRecord record);
}
