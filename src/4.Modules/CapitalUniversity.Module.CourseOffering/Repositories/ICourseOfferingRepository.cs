using CapitalUniversity.Core.Abstractions.Shared;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using CourseOfferingEntity = CapitalUniversity.Modules.CourseOffering.Domain.CourseOffering;

namespace CapitalUniversity.Modules.CourseOffering.Repositories;

public interface ICourseOfferingRepository
{
    Task<CourseOfferingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Cross-node paged search. Single SQL query.</summary>
    Task<PagedResult<CourseOfferingEntity>> SearchAsync(CourseOfferingSearchQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Slim list of offerings for a (node, semester). Optional <paramref name="status"/>
    /// narrows the result to one lifecycle stage — the admin "show me draft
    /// offerings only" filter. Default <c>null</c> keeps the previous behavior
    /// (return every status) so existing callers are unaffected.
    /// </summary>
    Task<IReadOnlyList<CourseOfferingEntity>> GetForNodeSemesterAsync(
        Guid structureNodeId,
        Guid semesterId,
        OfferingStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>All sections of one course in one academic term, across structure-node targets. Useful when an admin needs to see "where is CS101 running this term?" before opening another section.</summary>
    Task<IReadOnlyList<CourseOfferingEntity>> GetForCourseAsync(
        Guid courseId,
        Guid semesterId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns true if a non-deleted offering already occupies the (course, semester, node, section) slot. Used as a create-time precheck on top of the DB unique index.</summary>
    Task<bool> SectionExistsAsync(Guid courseId, Guid semesterId, Guid structureNodeId, string sectionCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Race-safe bump of <c>RegisteredCount</c> by one. Uses load + guard +
    /// optimistic concurrency on <c>RowVersion</c> with a small retry budget:
    /// if another writer commits first, the EF <c>DbUpdateConcurrencyException</c>
    /// triggers a re-read and re-evaluation of the capacity / cancelled
    /// guards. Two callers racing for the last seat will see only one
    /// succeed; the other returns <c>false</c>. This is the production
    /// primitive a future Registration module is expected to call. The
    /// in-memory entity method <c>IncrementRegistration</c> stays for
    /// aggregate-level invariant tests and single-request paths.
    /// </summary>
    /// <returns><c>true</c> if a seat was claimed; <c>false</c> if the
    /// offering is full, cancelled, missing, or contention exceeded the retry
    /// budget (caller should surface as "try again").</returns>
    Task<bool> TryIncrementRegistrationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Race-safe drop of <c>RegisteredCount</c> by one. Floors at zero —
    /// returns <c>false</c> if the count is already zero so a double-drop
    /// cannot make the count negative.
    /// </summary>
    Task<bool> TryDecrementRegistrationAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(CourseOfferingEntity offering, CancellationToken cancellationToken = default);

    void Update(CourseOfferingEntity offering);

    void Delete(CourseOfferingEntity offering);
}
