using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Execution;
using CapitalUniversity.Core.Abstractions.CrossCutting.Localization;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Services.Outbox;

public class OutboxService : IOutbox
{
    private readonly CoreDbContext _dbContext;
    private readonly IExecutionContext _executionContext;
    private readonly ICurrentCultureService _cultureService;

    public OutboxService(
        CoreDbContext dbContext,
        IExecutionContext executionContext,
        ICurrentCultureService cultureService)
    {
        _dbContext = dbContext;
        _executionContext = executionContext;
        _cultureService = cultureService;
    }

    public Task EnqueueAsync<TPayload>(string messageType, TPayload payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("messageType must be supplied.", nameof(messageType));

        var row = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
            Payload = JsonSerializer.Serialize(payload),
            EnqueuedAt = DateTime.UtcNow,
            CorrelationId = _executionContext.RequestId,
            Culture = _cultureService.Language
        };

        // Stage only — the caller commits with their own SaveChanges so the outbox
        // row lands in the same transaction as the business state it accompanies.
        _dbContext.OutboxMessages.Add(row);
        return Task.CompletedTask;
    }
}
