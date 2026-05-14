using System;
using System.Collections.Generic;
<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.Common;
=======
using CapitalUniversity.Core.Domain.Shared;
>>>>>>> Stashed changes
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
    public Dictionary<string, object>? Metadata { get; set; }
}
