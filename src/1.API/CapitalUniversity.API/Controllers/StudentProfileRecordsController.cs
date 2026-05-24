using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation;
using CapitalUniversity.Modules.Student.Abstractions.StudentInformation.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/students/{studentId:guid}/profile-records")]
public class StudentProfileRecordsController : ControllerBase
{
    private readonly IStudentProfileService _service;

    public StudentProfileRecordsController(IStudentProfileService service)
    {
        _service = service;
    }

    /// <summary>
    /// All profile records for the student. Optional filters: <c>category</c>,
    /// <c>verifiedOnly</c>, <c>verifiedFrom</c>/<c>verifiedTo</c> (inclusive /
    /// exclusive). Filters apply in-memory after the per-student fetch — the
    /// per-student record count is bounded so this is cheaper than a separate
    /// query path.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionNames.StudentProfileRecords.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetForStudent(
        Guid studentId,
        [FromQuery] StudentProfileCategory? category,
        [FromQuery] bool? verifiedOnly,
        [FromQuery] DateTime? verifiedFrom,
        [FromQuery] DateTime? verifiedTo,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetForStudentAsync(studentId, cancellationToken);

        IEnumerable<StudentProfileRecordResponse> filtered = result;
        if (category.HasValue) filtered = filtered.Where(r => r.Category == category.Value);
        if (verifiedOnly == true) filtered = filtered.Where(r => r.VerifiedAt.HasValue);
        if (verifiedFrom.HasValue) filtered = filtered.Where(r => r.VerifiedAt.HasValue && r.VerifiedAt.Value >= verifiedFrom.Value);
        if (verifiedTo.HasValue) filtered = filtered.Where(r => r.VerifiedAt.HasValue && r.VerifiedAt.Value < verifiedTo.Value);

        return Ok(filtered.ToList());
    }

    [HttpGet("by-category/{category}")]
    [HasPermission(PermissionNames.StudentProfileRecords.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetByCategory(Guid studentId, StudentProfileCategory category, [FromQuery] string? customKey, CancellationToken cancellationToken)
    {
        var result = await _service.GetForStudentCategoryAsync(studentId, category, customKey, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("{recordId:guid}")]
    [HasPermission(PermissionNames.StudentProfileRecords.View, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> GetById(Guid studentId, Guid recordId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(recordId, cancellationToken);
        if (result is null || result.StudentId != studentId) return NotFound();
        return Ok(result);
    }

    // L2 — PUT is intentional here. The natural key
    // (StudentId + Category [+ CustomCategoryKey]) makes the resource
    // idempotent under repeat: the same payload sent twice converges to the
    // same single canonical row. That is exactly the contract PUT promises
    // ("send the desired state of the resource at this URL"), so we keep PUT
    // rather than switching to POST. The route does not include a record id
    // because the natural key identifies the resource.
    [HttpPut]
    [HasPermission(PermissionNames.StudentProfileRecords.Insert, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> Upsert(Guid studentId, [FromBody] UpsertStudentProfileRecordRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertAsync(studentId, request, cancellationToken);
        return Ok(new { id, Message = "Profile record persisted" });
    }

    [HttpPost("{recordId:guid}/verify")]
    [HasPermission(PermissionNames.StudentProfileRecords.EditClose, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> Verify(Guid studentId, Guid recordId, [FromBody] VerifyStudentProfileRecordRequest request, CancellationToken cancellationToken)
    {
        await _service.VerifyAsync(studentId, recordId, request, cancellationToken);
        return Ok(new { Message = "Profile record verified" });
    }

    [HttpDelete("{recordId:guid}")]
    [HasPermission(PermissionNames.StudentProfileRecords.Delete, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> Delete(Guid studentId, Guid recordId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(studentId, recordId, cancellationToken);
        return Ok(new { Message = "Profile record deleted" });
    }

    /// <summary>
    /// 3.5 — bulk upsert. Body: <c>{ records: [UpsertStudentProfileRecordRequest...] }</c>.
    /// Independent per-row commits — see <see cref="BulkActionResult"/>.
    /// Failure ids are synthetic per-row slots (no entity id exists yet).
    /// </summary>
    [HttpPut("batch")]
    [HasPermission(PermissionNames.StudentProfileRecords.Insert, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> BatchUpsert(Guid studentId, [FromBody] BatchUpsertProfileRecordsRequest request, CancellationToken cancellationToken)
    {
        if (request?.Records is null || request.Records.Count == 0)
        {
            return BadRequest(new { Message = "At least one record is required." });
        }
        if (request.Records.Count > BulkConstants.MaxBulkSize)
        {
            return BadRequest(new { Message = $"Cannot upsert more than {BulkConstants.MaxBulkSize} records in one request." });
        }
        var result = await _service.BatchUpsertAsync(studentId, request.Records, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// 3.6 — bulk verify. Body: <c>{ recordIds: Guid[], verifiedBy: Guid }</c>.
    /// </summary>
    [HttpPost("verify")]
    [HasPermission(PermissionNames.StudentProfileRecords.EditClose, PermissionScopeKind.Student, "studentId")]
    public async Task<IActionResult> BatchVerify(Guid studentId, [FromBody] BatchVerifyProfileRecordsRequest request, CancellationToken cancellationToken)
    {
        if (request?.RecordIds is null || request.RecordIds.Count == 0)
        {
            return BadRequest(new { Message = "At least one record id is required." });
        }
        if (request.RecordIds.Count > BulkConstants.MaxBulkSize)
        {
            return BadRequest(new { Message = $"Cannot verify more than {BulkConstants.MaxBulkSize} records in one request." });
        }
        if (request.VerifiedBy == Guid.Empty)
        {
            return BadRequest(new { Message = "verifiedBy is required." });
        }
        var result = await _service.BatchVerifyAsync(studentId, request.RecordIds, request.VerifiedBy, cancellationToken);
        return Ok(result);
    }

    public sealed class BatchUpsertProfileRecordsRequest
    {
        public IReadOnlyList<UpsertStudentProfileRecordRequest> Records { get; init; } = Array.Empty<UpsertStudentProfileRecordRequest>();
    }

    public sealed class BatchVerifyProfileRecordsRequest
    {
        public IReadOnlyList<Guid> RecordIds { get; init; } = Array.Empty<Guid>();
        public Guid VerifiedBy { get; init; }
    }
}
