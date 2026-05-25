using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.Schedule.Abstractions;
using CapitalUniversity.Modules.Schedule.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/schedule-slots")]
public class ScheduleSlotsController : ControllerBase
{
    private readonly IScheduleSlotService _service;

    public ScheduleSlotsController(IScheduleSlotService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.Schedule.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("offering/{offeringId:guid}")]
    [HasPermission(PermissionNames.Schedule.View)]
    public async Task<IActionResult> GetForOffering(Guid offeringId, CancellationToken cancellationToken)
    {
        var result = await _service.GetForOfferingAsync(offeringId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cross-offering slot search. Filters: <c>courseOfferingId</c>,
    /// <c>dayOfWeek</c>, <c>kind</c>, <c>from/to</c> (start time-of-day window).
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionNames.Schedule.View)]
    public async Task<IActionResult> Search([FromQuery] ScheduleSlotSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.Schedule.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateScheduleSlotRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Schedule slot created successfully" });
    }

    [HttpPatch("{id:guid}")]
    [HasPermission(PermissionNames.Schedule.EditClose)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleSlotRequest request, CancellationToken cancellationToken)
    {
        await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(new { Message = "Schedule slot updated successfully" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PermissionNames.Schedule.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(id, cancellationToken);
        return Ok(new { Message = "Schedule slot deleted successfully" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.Schedule.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.CloseRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Schedule slot closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.Schedule.Open)]
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.OpenRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Schedule slot reopened" });
    }

    /// <summary>
    /// Atomic bulk-create of slots under one parent offering. Either every slot
    /// lands or none do — useful for the common "lecture + lab + recitation"
    /// block. Intra-batch overlap is validated in addition to overlap against
    /// existing siblings.
    /// </summary>
    [HttpPost("batch")]
    [HasPermission(PermissionNames.Schedule.Insert)]
    public async Task<IActionResult> BatchCreate([FromBody] BatchCreateScheduleSlotsRequest request, CancellationToken cancellationToken)
    {
        BulkRequestGuard.EnsureRecords(request?.Slots, propertyName: "slots");

        var ids = await _service.BatchCreateAsync(request!, cancellationToken);
        return Ok(new { Ids = ids, Message = "Schedule slots created" });
    }
}
