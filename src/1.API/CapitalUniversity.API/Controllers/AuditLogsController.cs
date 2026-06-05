using System;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.API.Infrastructure;
using CapitalUniversity.Core.Abstractions.CrossCutting.Auth.Authorization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CapitalUniversity.API.Controllers;

/// <summary>
/// Read-only admin access to the Mongo audit trail. Supports filtering by the
/// three "type" dimensions (category / action / severity), the audited entity,
/// the actor's role, and the actor's full name — plus a date range — all paged
/// server-side. Gated behind <c>system.audit-logs.View</c>.
/// </summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogReader _reader;

    public AuditLogsController(IAuditLogReader reader)
    {
        _reader = reader;
    }

    [HttpGet]
    [HasPermission(PermissionNames.AuditLogs.View)]
    public async Task<ActionResult<AuditLogPage>> Get([FromQuery] AuditLogFilterRequest request, CancellationToken cancellationToken)
    {
        var result = await _reader.QueryAsync(request.ToQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Query-string shape. Plain get/set so ASP.NET model binding populates it,
    /// then mapped to the immutable <see cref="AuditLogQuery"/>.
    /// </summary>
    public sealed class AuditLogFilterRequest
    {
        public LogCategory? Category { get; set; }
        public string? Action { get; set; }
        public LogLevelType? Level { get; set; }
        public string? EntityName { get; set; }
        public string? Role { get; set; }
        public string? UserName { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;

        public AuditLogQuery ToQuery() => new()
        {
            Category = Category,
            Action = Action,
            Level = Level,
            EntityName = EntityName,
            Role = Role,
            UserName = UserName,
            FromUtc = FromUtc,
            ToUtc = ToUtc,
            Page = Page,
            PageSize = PageSize,
        };
    }
}
