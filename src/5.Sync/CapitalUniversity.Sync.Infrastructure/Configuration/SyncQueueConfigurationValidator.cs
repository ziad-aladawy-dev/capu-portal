using CapitalUniversity.Sync.Abstractions.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CapitalUniversity.Sync.Infrastructure.Configuration;

/// <summary>
/// Fail-fast validator for queue configuration. Runs once at host startup BEFORE any
/// Hangfire worker dequeues. Catches two classes of silent operational misconfiguration:
///
/// <list type="bullet">
///   <item>
///     <b>Legacy <c>:</c> separator in directional <see cref="SyncOptions.ModuleQueues"/>
///     keys.</b> Phase 6 changed the direction separator from <c>:</c> to <c>|</c>
///     because <c>:</c> is the .NET configuration path separator (it collided with the
///     module's own plain entry). An operator who copies a pre-Phase-6 environment
///     override (e.g. <c>"students:Push": "push-sync"</c>) would see push runs silently
///     fall back to the default queue lookup. We refuse to start the host instead.
///   </item>
///   <item>
///     <b>Dispatch queue not in the Hangfire listen list.</b> If
///     <c>Sync:ModuleQueues["finance"] = "finance-sync"</c> but the Hangfire server
///     wasn't started with <c>finance-sync</c> in its <c>Queues</c> array, jobs land
///     in the queue and no worker dequeues them. The runs sit in <c>Enqueued</c> with
///     no error, only an absence of progress. We reject this at startup.
///   </item>
/// </list>
///
/// <para>
/// Both classes of failure are silent at the Hangfire layer but loud at boot.
/// </para>
/// </summary>
public sealed class SyncQueueConfigurationValidator : IHostedService
{
    private const string LegacyDirectionSeparator = ":";
    private const string CurrentDirectionSeparator = "|";

    private readonly IOptions<SyncOptions> _options;
    private readonly IConfiguration _configuration;
    private readonly ISyncLogger _logger;

    public SyncQueueConfigurationValidator(
        IOptions<SyncOptions> options,
        IConfiguration configuration,
        ISyncLogger logger)
    {
        _options = options;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = _options.Value;
        var listenList = opts.Hangfire.Queues ?? new List<string>();
        var listenSet = new HashSet<string>(listenList, StringComparer.OrdinalIgnoreCase)
        {
            opts.DefaultQueue
        };

        var problems = new List<string>();

        // 1. RAW configuration scan for legacy ':' separator keys. The bound
        //    Dictionary<string,string> can't see these because the .NET configuration
        //    binder splits the JSON key "students:Push" on the ':' and treats it as a
        //    nested path — the original key disappears at binding time. So we walk
        //    the raw config tree and flag any "Sync:ModuleQueues:{module}:{direction}"
        //    paths that exist alongside a "Sync:ModuleQueues:{module}" string value.
        var moduleQueuesSection = _configuration.GetSection($"{SyncOptions.SectionName}:ModuleQueues");
        foreach (var moduleEntry in moduleQueuesSection.GetChildren())
        {
            // Any child of a ModuleQueues entry indicates a nested directional override
            // whose JSON key was written with ':' instead of the current '|' separator.
            foreach (var nested in moduleEntry.GetChildren())
            {
                problems.Add(
                    $"Sync:ModuleQueues key '{moduleEntry.Key}:{nested.Key}' uses the legacy ':' direction separator. " +
                    $"Use '|' instead (i.e. '{moduleEntry.Key}|{nested.Key}': '{nested.Value}'). " +
                    "The ':' character collides with the .NET configuration path separator and the entry will not bind " +
                    "— directional queue overrides silently fall back to the module-level queue or the default.");
            }
        }

        // 2. Bound-options scan for dispatch targets the Hangfire server isn't listening on.
        foreach (var (key, target) in opts.ModuleQueues)
        {
            if (!listenSet.Contains(target))
            {
                problems.Add(
                    $"Sync:ModuleQueues['{key}'] dispatches to '{target}' but that queue is not " +
                    $"in Sync:Hangfire:Queues ({string.Join(", ", listenList)}) or the default ('{opts.DefaultQueue}'). " +
                    "Add it to the listen list or change the dispatch target.");
            }
        }

        if (!listenSet.Contains(opts.DefaultQueue))
        {
            problems.Add(
                $"Sync:DefaultQueue '{opts.DefaultQueue}' is not in Sync:Hangfire:Queues. " +
                "The default queue must be listened on; otherwise unrouted modules will silently stick in Enqueued.");
        }

        // 3. Per-queue worker-pool sanity. Every queue in every pool must be in the
        //    overall listen list; pools must be pairwise disjoint or a queue would be
        //    double-dequeued.
        var pools = opts.Hangfire.QueuePools ?? new List<SyncHangfireQueuePool>();
        if (pools.Count > 0)
        {
            var seenAcrossPools = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < pools.Count; i++)
            {
                var pool = pools[i];
                var poolName = pool.Name ?? $"#{i}";

                if (pool.Queues is null || pool.Queues.Count == 0)
                {
                    problems.Add($"Sync:Hangfire:QueuePools[{poolName}] declares no queues.");
                    continue;
                }

                if (pool.WorkerCount < 1)
                {
                    problems.Add(
                        $"Sync:Hangfire:QueuePools[{poolName}].WorkerCount must be >= 1 " +
                        $"(was {pool.WorkerCount}).");
                }

                foreach (var queue in pool.Queues)
                {
                    if (!listenSet.Contains(queue))
                    {
                        problems.Add(
                            $"Sync:Hangfire:QueuePools[{poolName}] includes '{queue}' which is not " +
                            $"in Sync:Hangfire:Queues ({string.Join(", ", listenList)}). " +
                            "Pool queues must be a subset of the host's listen list.");
                    }

                    if (seenAcrossPools.TryGetValue(queue, out var prior))
                    {
                        problems.Add(
                            $"Sync:Hangfire:QueuePools overlap on queue '{queue}': declared by both " +
                            $"'{prior}' and '{poolName}'. Pools must be pairwise disjoint or a single " +
                            "queue would be served by two BackgroundJobServer instances simultaneously.");
                    }
                    else
                    {
                        seenAcrossPools[queue] = poolName;
                    }
                }
            }

            // If pools are configured, every listen-list queue should also be covered by
            // exactly one pool. A queue in the listen list but not in any pool would be
            // silently un-served (the legacy single-server fallback only fires when pools
            // is empty).
            foreach (var queue in listenList)
            {
                if (!seenAcrossPools.ContainsKey(queue))
                {
                    problems.Add(
                        $"Sync:Hangfire:Queues contains '{queue}' but no Sync:Hangfire:QueuePools entry " +
                        "covers it. With QueuePools configured, every listen-list queue must belong to " +
                        "exactly one pool — otherwise the queue has no worker and jobs sit in Enqueued.");
                }
            }
        }

        if (problems.Count == 0)
        {
            _logger.LogInformation(Guid.Empty,
                "Sync queue configuration validated. ListenQueues=[{Queues}] ModuleQueueOverrides={OverrideCount}.",
                string.Join(",", listenList), opts.ModuleQueues.Count);

            // Non-fatal advisory. The pipeline's per-batch writer retry stacks
            // multiplicatively with the writers' own internal retries; an operator
            // who flips this on without reading the XML doc can end up running
            // (1 + N) × writer.MaxAttempts tries per batch per Hangfire attempt.
            // We don't refuse to start — the layering is documented as a valid
            // tuning option — but a loud startup line gives the operator a chance
            // to notice the choice in audit before it shows up under load.
            if (opts.Pipeline.PerBatchWriterRetryAttempts > 0)
            {
                _logger.LogWarning(Guid.Empty,
                    "Sync:Pipeline:PerBatchWriterRetryAttempts={Attempts} is non-zero — pipeline retry stacks with writer-internal retry. " +
                    "Total per-batch tries = (1 + {Attempts}) × writer.MaxAttempts per Hangfire attempt. " +
                    "Verify intentional; see SyncPipelineOptions XML for layering guidance.",
                    opts.Pipeline.PerBatchWriterRetryAttempts,
                    opts.Pipeline.PerBatchWriterRetryAttempts);
            }

            return Task.CompletedTask;
        }

        var message = "Sync queue configuration invalid — refusing to start: " +
                      string.Join(" | ", problems);
        _logger.LogError(Guid.Empty, null, "{Message}", message);
        throw new InvalidOperationException(message);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}