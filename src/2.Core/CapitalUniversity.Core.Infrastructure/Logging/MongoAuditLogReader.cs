using CapitalUniversity.Core.Abstractions.CrossCutting.Logging;
using CapitalUniversity.Core.Domain.Logging;
using CapitalUniversity.Core.Infrastructure.Persistence.Mongo;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace CapitalUniversity.Core.Infrastructure.Logging;

/// <summary>
/// Mongo-backed <see cref="IAuditLogReader"/>. Translates an
/// <see cref="AuditLogQuery"/> into a server-side filter + sort + skip/take so
/// paging happens in the database, and projects matched <see cref="LogEntry"/>
/// documents into <see cref="AuditLogDto"/>.
/// </summary>
public sealed class MongoAuditLogReader : IAuditLogReader
{
    private const int MaxPageSize = 200;

    private readonly IMongoCollection<LogEntry> _collection;

    public MongoAuditLogReader(IMongoClient mongoClient, IOptions<MongoSettings> mongoSettings)
    {
        var settings = mongoSettings.Value;
        var db = mongoClient.GetDatabase(settings.DatabaseName);
        _collection = db.GetCollection<LogEntry>(settings.LogsCollection);
    }

    public async Task<AuditLogPage> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var b = Builders<LogEntry>.Filter;
        var filters = new List<FilterDefinition<LogEntry>>();

        if (query.Category.HasValue) filters.Add(b.Eq(x => x.Category, query.Category.Value));
        if (query.Level.HasValue) filters.Add(b.Eq(x => x.Level, query.Level.Value));
        if (!string.IsNullOrWhiteSpace(query.Action)) filters.Add(b.Regex(x => x.Action, Exact(query.Action)));
        if (!string.IsNullOrWhiteSpace(query.EntityName)) filters.Add(b.Regex(x => x.EntityName, Exact(query.EntityName)));
        if (!string.IsNullOrWhiteSpace(query.Role)) filters.Add(b.Regex(x => x.Role, Exact(query.Role)));
        if (!string.IsNullOrWhiteSpace(query.UserName)) filters.Add(b.Regex(x => x.UserName, Contains(query.UserName)));
        if (query.FromUtc.HasValue) filters.Add(b.Gte(x => x.CreatedAtUtc, query.FromUtc.Value));
        if (query.ToUtc.HasValue) filters.Add(b.Lt(x => x.CreatedAtUtc, query.ToUtc.Value));

        var filter = filters.Count > 0 ? b.And(filters) : b.Empty;

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 1 : Math.Min(query.PageSize, MaxPageSize);

        var total = await _collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var docs = await _collection
            .Find(filter)
            .SortByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new AuditLogPage
        {
            Items = docs.Select(Map).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    // Anchored, case-insensitive exact match — Role/Action/EntityName are known
    // discrete values, so we match the whole string ignoring case.
    private static MongoDB.Bson.BsonRegularExpression Exact(string value) =>
        new("^" + System.Text.RegularExpressions.Regex.Escape(value) + "$", "i");

    private static MongoDB.Bson.BsonRegularExpression Contains(string value) =>
        new(System.Text.RegularExpressions.Regex.Escape(value), "i");

    private static AuditLogDto Map(LogEntry e) => new()
    {
        Id = e.Id,
        CreatedAtUtc = e.CreatedAtUtc,
        Category = e.Category,
        Level = e.Level,
        Action = e.Action,
        EntityName = e.EntityName,
        Source = e.Source,
        Message = e.Message,
        UserId = e.UserId,
        UserName = e.UserName,
        Role = e.Role,
        IpAddress = e.IpAddress,
        RequestPath = e.RequestPath,
        HttpMethod = e.HttpMethod,
        CorrelationId = e.CorrelationId,
        ExceptionMessage = e.ExceptionMessage,
        Metadata = e.Metadata,
    };
}
