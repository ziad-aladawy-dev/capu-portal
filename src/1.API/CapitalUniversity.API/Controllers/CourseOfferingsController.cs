using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.CourseOffering.Abstractions;
using CapitalUniversity.Modules.CourseOffering.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/course-offerings")]
public class CourseOfferingsController : ControllerBase
{
    private readonly ICourseOfferingService _service;

    public CourseOfferingsController(ICourseOfferingService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("node/{nodeId:guid}/semester/{semesterId:guid}")]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> GetForNode(Guid nodeId, Guid semesterId, [FromQuery] OfferingStatus? status, CancellationToken cancellationToken)
    {
        var result = await _service.GetForNodeSemesterAsync(nodeId, semesterId, status, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cross-node offering search. Filters: <c>semesterId</c>, <c>structureNodeId</c>,
    /// <c>courseId</c>, <c>status</c>, <c>registrationState</c>, free-text <c>search</c>
    /// (section code). Sort: <c>createdAt|sectionCode|status</c>.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> Search([FromQuery] CourseOfferingSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.CourseOfferings.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateCourseOfferingRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Course offering created successfully" });
    }

    /// <summary>
    /// Batch section creation. Returns HTTP 200 even with partial failure —
    /// the response body carries created (id, sectionCode) pairs and per-section
    /// failure reasons. Section codes are generated server-side.
    /// </summary>
    [HttpPost("batch")]
    [HasPermission(PermissionNames.CourseOfferings.Insert)]
    public async Task<IActionResult> BatchCreate([FromBody] BatchCreateCourseOfferingsRequest request, CancellationToken cancellationToken)
    {
        if (request is null) return BadRequest(new { Message = "Request body is required" });
        var result = await _service.BatchCreateAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Aggregate offering counters for one term (optionally one node), scope-filtered.</summary>
    [HttpGet("stats")]
    [HasPermission(PermissionNames.CourseOfferings.View)]
    public async Task<IActionResult> GetStats([FromQuery] Guid semesterId, [FromQuery] Guid? structureNodeId, CancellationToken cancellationToken)
    {
        if (semesterId == Guid.Empty) return BadRequest(new { Message = "semesterId is required" });
        var result = await _service.GetStatsAsync(semesterId, structureNodeId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.CourseOfferings.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourseOfferingRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(new { Message = "Course offering updated successfully" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.CourseOfferings.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.CloseRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Course offering closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.CourseOfferings.Open)]
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.OpenRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Course offering reopened" });
    }

    /// <summary>
    /// Bulk-publishes offerings (Draft → Open). Returns HTTP 200 even with
    /// partial failure — the response body's <c>failures</c> array carries
    /// per-id reasons (NotFound / InvalidTransition / Conflict).
    /// </summary>
    [HttpPost("publish")]
    [HasPermission(PermissionNames.CourseOfferings.EditClose)]
    public async Task<IActionResult> BulkPublish([FromBody] BulkActionRequest request, CancellationToken cancellationToken)
    {
        BulkRequestGuard.EnsureValidIds(request?.Ids);
        var result = await _service.BulkPublishAsync(request!.Ids, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Bulk-cancels offerings. <c>reason</c> is required on the payload — it's
    /// accepted for audit purposes today and will be persisted when Phase-2
    /// audit work lands.
    /// </summary>
    [HttpPost("cancel")]
    [HasPermission(PermissionNames.CourseOfferings.EditClose)]
    public async Task<IActionResult> BulkCancel([FromBody] BulkActionRequest<BulkCancelPayload> request, CancellationToken cancellationToken)
    {
        BulkRequestGuard.EnsureValidIds(request?.Ids);
        BulkRequestGuard.EnsurePayload(request?.Payload);
        BulkRequestGuard.EnsureReason(request!.Payload!.Reason);

        var result = await _service.BulkCancelAsync(request.Ids, request.Payload.Reason, cancellationToken);
        return Ok(result);
    }

    /// <summary>Payload shape for bulk-cancel: a single required reason string.</summary>
    public sealed class BulkCancelPayload
    {
        public string Reason { get; init; } = string.Empty;
    }
}
