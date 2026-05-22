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
    public string Source { get; set; } = string.Empty;
    public string? ExceptionMessage { get; set; }
    public string? StackTrace { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? UserId { get; set; }
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
