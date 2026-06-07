using CapitalUniversity.Core.Abstractions.Sync;
using CapitalUniversity.Core.Domain.Common;
using CapitalUniversity.Modules.Payments.Abstractions.Treasury;
using CapitalUniversity.Modules.Payments.Domain.Treasury;
using Microsoft.Extensions.Logging;

namespace CapitalUniversity.Modules.Payments.Application.Treasury;

/// <summary>
/// Maps <see cref="TreasuryReceiptDto"/> (already ConnectionTypeId=6 filtered by
/// the client) to <see cref="TreasuryReceipt"/> and merges through
/// <see cref="ICoreWriteGateway"/> using the platform's external-wins upsert.
/// The gateway commits the batch; this service owns no transaction.
/// </summary>
public sealed class ReceiptSyncService : IReceiptSyncService
{
    private readonly ITreasuryClient _client;
    private readonly ICoreWriteGateway _gateway;
    private readonly ILogger<ReceiptSyncService> _logger;

    public ReceiptSyncService(
        ITreasuryClient client,
        ICoreWriteGateway gateway,
        ILogger<ReceiptSyncService> logger)
    {
        _client = client;
        _gateway = gateway;
        _logger = logger;
    }

    public async Task<int> SyncAsync(CancellationToken cancellationToken = default)
    {
        var dtos = await _client.GetReceiptsAsync(cancellationToken);
        if (dtos.Count == 0)
        {
            _logger.LogInformation("Treasury receipt sync: no relevant (ConnectionTypeId=6) receipts returned.");
            return 0;
        }

        var entities = dtos.Select(MapToEntity).ToList();

        var result = await _gateway.UpsertAsync<TreasuryReceipt>(
            entities,
            ApplyUpdate,
            new CoreUpsertOptions { AllowInsert = true, RespectExternalUpdatedAt = true },
            cancellationToken);

        _logger.LogInformation(
            "Treasury receipt sync: {Persisted} persisted, {NotNewer} skipped(not newer), {NoExtId} skipped(no external id).",
            result.Persisted, result.SkippedNotNewer, result.SkippedNoExternalId);

        return result.Persisted;
    }

    private static TreasuryReceipt MapToEntity(TreasuryReceiptDto d) => new()
    {
        ExternalReceiptId = d.Id,
        ConnectionTypeId = d.ConnectionTypeId,
        Name = d.Name,
        UnitAmount = d.Amount,
        Currency = d.Currency,
        IsActive = d.IsActive,
        ExternallySourced = new ExternallySourcedData
        {
            ExternalId = d.Id,
            ExternalUpdatedAt = d.UpdatedAt,
        },
    };

    private static void ApplyUpdate(TreasuryReceipt existing, TreasuryReceipt incoming)
    {
        existing.ExternalReceiptId = incoming.ExternalReceiptId;
        existing.ConnectionTypeId = incoming.ConnectionTypeId;
        existing.Name = incoming.Name;
        existing.UnitAmount = incoming.UnitAmount;
        existing.Currency = incoming.Currency;
        existing.IsActive = incoming.IsActive;
    }
}
