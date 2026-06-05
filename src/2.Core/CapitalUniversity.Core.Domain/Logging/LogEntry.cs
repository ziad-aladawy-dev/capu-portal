using System;
using System.Collections.Generic;
using CapitalUniversity.Core.Domain.Common;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


namespace CapitalUniversity.Core.Domain.Logging;

public class LogEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public LogLevelType Level { get; set; }

    /// <summary>
    /// Origin classification (Data / Auth / Sync / Error). Top-level so the audit
    /// read API can filter by "type" without cracking open <see cref="Metadata"/>.
    /// Lifted from <c>AuditMetadataKeys.Category</c> by the logger; defaults to
    /// <see cref="LogCategory.Data"/> for Info and <see cref="LogCategory.Error"/>
    /// for Warning/Error/Critical when a producer doesn't set it.
    /// </summary>
    public LogCategory Category { get; set; }

    /// <summary>
    /// Friendly action verb for data-change entries (Created / Updated / Deleted).
    /// Null for non-entity entries (auth events, sync diagnostics). Lifted from
    /// <c>AuditMetadataKeys.Action</c>.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// The audited entity type name (e.g. <c>Invoice</c>, <c>ScheduleSlot</c>) for
    /// data-change entries. Null otherwise. Lifted from <c>AuditMetadataKeys.Entity</c>.
    /// </summary>
    public string? EntityName { get; set; }

    public string Source { get; set; } = string.Empty;
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }

    /// <summary>Actor's display (full) name, captured from the <c>name</c> claim at log time. Null for system/background actions.</summary>
    public string? UserName { get; set; }

    /// <summary>Actor's role name, captured from the role claim at log time. Null for system/background actions.</summary>
    public string? Role { get; set; }

    public string? IpAddress { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }

    /// <summary>
    /// Correlation id captured from <c>HttpContext.Items</c> at log time.
    /// Populated by the buffered logger on the request thread so the value
    /// survives the async hand-off to the Mongo flush worker — without it,
    /// audit entries can't be joined back to the Serilog text logs that
    /// share the same correlation id via the middleware's log scope.
    /// Null when the log call originated outside an HTTP request.
    /// </summary>
    public string? CorrelationId { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }
}
