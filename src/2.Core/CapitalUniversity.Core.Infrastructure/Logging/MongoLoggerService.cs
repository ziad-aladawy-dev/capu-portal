using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;

<<<<<<< Updated upstream
using CapitalUniversity.Core.Domain.Common;

=======
>>>>>>> Stashed changes
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Domain.Shared;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CapitalUniversity.Core.Infrastructure.Logging;

public class MongoLoggerService : IAppLogger
{
    private readonly IMongoCollection<LogEntry> _logsCollection;

    public MongoLoggerService(IMongoClient mongoClient, IOptions<MongoSettings> mongoSettings)
    {
        var settings = mongoSettings.Value;
        var database = mongoClient.GetDatabase(settings.DatabaseName);
        _logsCollection = database.GetCollection<LogEntry>(settings.LogsCollection);
    }

    private string? GetClientIp(HttpContext? context)
    {
        if (context == null) return null;

        string? ip = null;

        // 1. Check X-Forwarded-For
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor.ToString().Split(',')[0].Trim();
        }

        // 2. Check X-Real-IP
        if (string.IsNullOrEmpty(ip) && context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            ip = realIp.ToString().Trim();
        }

        // 3. Fallback to RemoteIpAddress
        if (string.IsNullOrEmpty(ip) && context.Connection.RemoteIpAddress != null)
        {
            ip = context.Connection.RemoteIpAddress.ToString();
        }

        // Convert IPv6 localhost to IPv4 localhost for readability
        if (ip == "::1")
        {
            ip = "127.0.0.1";
        }

        return ip;
    }

    private LogEntry CreateLogEntry(
        LogLevelType level,
        string message,
        string source,
        HttpContext? context,
        Exception? exception = null,
        Dictionary<string, object>? metadata = null)
    {
        var entry = new LogEntry
        {
            Id = Guid.NewGuid(),
            Level = level,
            Message = message,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow,
            Metadata = metadata
        };

        if (exception != null)
        {
            entry.ExceptionMessage = exception.Message;
            entry.StackTrace = exception.StackTrace;
        }

        if (context != null)
        {
            entry.IpAddress = GetClientIp(context);
            entry.RequestPath = context.Request.Path;
            entry.HttpMethod = context.Request.Method;
            entry.UserId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        return entry;
    }

    public async Task LogInfoAsync(string message, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        var entry = CreateLogEntry(LogLevelType.Info, message, source, context, null, metadata);
        await _logsCollection.InsertOneAsync(entry);
    }

    public async Task LogWarningAsync(string message, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        var entry = CreateLogEntry(LogLevelType.Warning, message, source, context, null, metadata);
        await _logsCollection.InsertOneAsync(entry);
    }

    public async Task LogErrorAsync(string message, Exception exception, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        var entry = CreateLogEntry(LogLevelType.Error, message, source, context, exception, metadata);
        await _logsCollection.InsertOneAsync(entry);
    }

    public async Task LogCriticalAsync(string message, Exception exception, string source, HttpContext? context = null, Dictionary<string, object>? metadata = null)
    {
        var entry = CreateLogEntry(LogLevelType.Critical, message, source, context, exception, metadata);
        await _logsCollection.InsertOneAsync(entry);
    }
}

