using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapitalUniversity.Core.Domain.Common.Enums;
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace CapitalUniversity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogsController : ControllerBase
{
    private readonly IMongoCollection<LogEntry> _logsCollection;

    public LogsController(IMongoClient mongoClient, IOptions<MongoSettings> mongoSettings)
    {
        var settings = mongoSettings.Value;
        var database = mongoClient.GetDatabase(settings.DatabaseName);
        _logsCollection = database.GetCollection<LogEntry>(settings.LogsCollection);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllLogsAsync()
    {
        var logs = await _logsCollection.Find(_ => true)
                                        .SortByDescending(l => l.CreatedAtUtc)
                                        .ToListAsync();
        return Ok(logs);
    }

    [HttpGet("filter")]
    public async Task<IActionResult> FilterLogsAsync(
        [FromQuery] LogLevelType? level,
        [FromQuery] string? userId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var filterBuilder = Builders<LogEntry>.Filter;
        var filter = filterBuilder.Empty;

        if (level.HasValue)
        {
            filter &= filterBuilder.Eq(l => l.Level, level.Value);
        }

        if (!string.IsNullOrEmpty(userId))
        {
            filter &= filterBuilder.Eq(l => l.UserId, userId);
        }

        if (startDate.HasValue)
        {
            filter &= filterBuilder.Gte(l => l.CreatedAtUtc, startDate.Value);
        }

        if (endDate.HasValue)
        {
            filter &= filterBuilder.Lte(l => l.CreatedAtUtc, endDate.Value);
        }

        var logs = await _logsCollection.Find(filter)
                                        .SortByDescending(l => l.CreatedAtUtc)
                                        .ToListAsync();
        return Ok(logs);
    }
}
