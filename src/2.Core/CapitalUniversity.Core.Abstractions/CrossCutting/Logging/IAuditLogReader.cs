using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapitalUniversity.Core.Domain.Common;

namespace CapitalUniversity.Core.Abstractions.CrossCutting.Logging;

/// <summary>
/// Read-side over the Mongo audit trail. Backs the admin audit endpoint with
/// server-side filtering + paging so the (potentially large) log collection is
/// never pulled into memory wholesale.
/// </summary>
public interface IAuditLogReader
{
    Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filter + paging criteria for an audit-log query. Every filter is optional and
/// combined with AND. The three "type" dimensions are orthogonal:
/// <see cref="Category"/> (origin), <see cref="Action"/> (Created/Updated/Deleted),
/// and <see cref="Level"/> (severity).
/// </summary>
public sealed class AuditLogQuery
{
    /// <summary>Origin filter — Data / Auth / Sync / Error.</summary>
    public LogCategory? Category { get; init; }

    /// <summary>Action-verb filter for data-change entries (Created / Updated / Deleted). Case-insensitive exact match.</summary>
    public string? Action { get; init; }

    /// <summary>Severity filter — Info / Warning / Error / Critical.</summary>
    public LogLevelType? Level { get; init; }

    /// <summary>Audited entity type name (e.g. <c>Invoice</c>). Case-insensitive exact match.</summary>
    public string? EntityName { get; init; }

    /// <summary>Actor's role name. Case-insensitive exact match.</summary>
    public string? Role { get; init; }

    /// <summary>Actor's full name. Case-insensitive substring match.</summary>
    public string? UserName { get; init; }

    /// <summary>Inclusive lower bound on <c>CreatedAtUtc</c>.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Exclusive upper bound on <c>CreatedAtUtc</c>.</summary>
    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class AuditLogPage
{
    public IReadOnlyList<AuditLogDto> Items { get; init; } = Array.Empty<AuditLogDto>();
    public long Total { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class AuditLogDto
{
    public Guid Id { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public LogCategory Category { get; init; }
    public LogLevelType Level { get; init; }
    public string? Action { get; init; }
    public string? EntityName { get; init; }
    public string Source { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? Role { get; init; }
    public string? IpAddress { get; init; }
    public string? RequestPath { get; init; }
    public string? HttpMethod { get; init; }
    public string? CorrelationId { get; init; }
    public string? ExceptionMessage { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}
