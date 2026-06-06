using CapitalUniversity.Sync.Abstractions.Contracts;
using CapitalUniversity.Sync.Abstractions.Models;
using CapitalUniversity.Sync.Finance.Configuration;
using CapitalUniversity.Sync.Finance.Domain;
using CapitalUniversity.Sync.Finance.Pull;
using CapitalUniversity.Sync.Finance.Push;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Finance;

/// <summary>
/// Finance sync — syncs <see cref="ExternalInvoice"/> headers (mirrors Core
/// <c>Modules.Payments.Domain.Invoice</c>). Line items belong on a separate
/// pipeline if they need to be synced.
/// </summary>
public sealed class FinanceSyncModule : ISyncModule
{
    public const string Name = "finance";

    private readonly ISyncPipeline _pipeline;
    private readonly ISyncLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<FinanceSyncOptions> _options;

    public FinanceSyncModule(
        ISyncPipeline pipeline,
        ISyncLogger logger,
        IServiceScopeFactory scopeFactory,
        IOptions<FinanceSyncOptions> options)
    {
        _pipeline = pipeline;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public string ModuleName => Name;

    public Task<SyncResult> PullAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPullAsync<ExternalInvoice, InvoiceSyncDispatch>(
            _scopeFactory, _logger, context, Name, _options.Value.BatchSize,
            externalKeySelector: t => t.ExternalInvoiceId,
            extractorFactory: sp => sp.GetRequiredService<InvoiceExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<InvoiceMapper>(),
            validatorFactory: sp => sp.GetRequiredService<InvoiceValidator>(),
            writerFactory:    sp => sp.GetRequiredService<InvoiceWriter>(),
            cancellationToken);

    public Task<SyncResult> PushAsync(SyncContext context, CancellationToken cancellationToken)
        => _pipeline.RunStandardPushAsync<InvoiceOutboxEntity, InvoiceOutboxDispatch>(
            _scopeFactory, context, _options.Value.PushBatchSize,
            externalKeySelector: r => r.Id.ToString(),
            extractorFactory: sp => sp.GetRequiredService<InvoiceOutboxExtractor>(),
            mapperFactory:    sp => sp.GetRequiredService<InvoiceOutboxMapper>(),
            validatorFactory: sp => sp.GetRequiredService<InvoiceOutboxValidator>(),
            writerFactory:    sp => sp.GetRequiredService<InvoiceOutboxWriter>(),
            cancellationToken);
}
