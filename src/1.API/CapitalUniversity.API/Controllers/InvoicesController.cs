using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.Shared.BulkActions;
using CapitalUniversity.Modules.Payments.Abstractions;
using CapitalUniversity.Modules.Payments.Abstractions.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;

    public InvoicesController(IInvoiceService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    [HasPermission(PermissionNames.Invoices.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("by-student/{studentId:guid}")]
    [HasPermission(PermissionNames.Invoices.View)]
    public async Task<IActionResult> GetForStudent(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _service.GetForStudentAsync(studentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Paged invoice list. Filters: <c>studentId</c>, <c>status</c>,
    /// <c>issuedFrom/To</c> (inclusive / exclusive), <c>dueFrom/To</c>,
    /// <c>minAmount</c>, <c>maxAmount</c>, <c>currency</c>, free-text <c>search</c>.
    /// Sort: <c>createdAt|dueAt|amount|status</c> with <c>:asc|:desc</c>.
    /// </summary>
    [HttpGet]
    [HasPermission(PermissionNames.Invoices.View)]
    public async Task<IActionResult> Search([FromQuery] InvoiceSearchQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.SearchAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [HasPermission(PermissionNames.Invoices.Insert)]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request, CancellationToken cancellationToken)
    {
        var id = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, new { id, Message = "Invoice created successfully" });
    }

    [HttpPost("{id:guid}/cancel")]
    [HasPermission(PermissionNames.Invoices.EditClose)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelInvoiceRequest request, CancellationToken cancellationToken)
    {
        await _service.CancelAsync(id, request, cancellationToken);
        return Ok(new { Message = "Invoice cancelled" });
    }

    [HttpPost("{id:guid}/close-record")]
    [HasPermission(PermissionNames.Invoices.EditClose)]
    public async Task<IActionResult> CloseRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.CloseRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Invoice closed" });
    }

    [HttpPost("{id:guid}/open-record")]
    [HasPermission(PermissionNames.Invoices.Open)]
    public async Task<IActionResult> OpenRecord(Guid id, CancellationToken cancellationToken)
    {
        await _service.OpenRecordAsync(id, cancellationToken);
        return Ok(new { Message = "Invoice reopened" });
    }

    /// <summary>
    /// Bulk-cancels invoices. <c>reason</c> is required. Returns HTTP 200 even
    /// with partial failure — the response body's <c>failures</c> array carries
    /// per-id reasons (NotFound for missing/out-of-scope; Conflict for
    /// Paid / Refunded / record-closed invoices). Already-Cancelled rows are a
    /// successful no-op so replays are safe.
    /// </summary>
    [HttpPost("cancel")]
    [HasPermission(PermissionNames.Invoices.EditClose)]
    public async Task<IActionResult> BulkCancel([FromBody] BulkActionRequest<BulkCancelInvoicesPayload> request, CancellationToken cancellationToken)
    {
        if (request is null || request.Ids is null || request.Ids.Count == 0)
        {
            return BadRequest(new { Message = "At least one invoice id is required." });
        }
        if (request.Ids.Count > BulkConstants.MaxBulkSize)
        {
            return BadRequest(new { Message = $"Cannot cancel more than {BulkConstants.MaxBulkSize} invoices in one request." });
        }
        if (request.Payload is null || string.IsNullOrWhiteSpace(request.Payload.Reason))
        {
            return BadRequest(new { Message = "A cancellation reason is required." });
        }

        var result = await _service.BulkCancelAsync(request.Ids, request.Payload.Reason, cancellationToken);
        return Ok(result);
    }

    /// <summary>Payload shape for bulk-cancel: a single required reason string.</summary>
    public sealed class BulkCancelInvoicesPayload
    {
        public string Reason { get; init; } = string.Empty;
    }
}
