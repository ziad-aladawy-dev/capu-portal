using System.Text.Json;
using CapitalUniversity.Core.Abstractions.CrossCutting.Outbox;
using CapitalUniversity.Core.Domain.Outbox;
using CapitalUniversity.Core.Infrastructure.Persistence;

namespace CapitalUniversity.Core.Infrastructure.Services.Outbox;

public class OutboxService : IOutbox
{
    private readonly CoreDbContext _dbContext;

    public OutboxService(CoreDbContext dbContext)
    {
        _dbContext = dbContext;
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
        };

        // Stage only — the caller commits with their own SaveChanges so the outbox
        // row lands in the same transaction as the business state it accompanies.
        _dbContext.OutboxMessages.Add(row);
        return Task.CompletedTask;
    }
}
