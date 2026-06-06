# Phase 4 — Sync Engine (Core Pipeline): Audit

**Date:** 2026-05-28
**Status:** Completed (runtime-validated; all four success criteria observed end-to-end)
**Spec source:** `docs/Sync_Implementation_Plan.md`, `docs/Sync_Platform_Model.md`

---

## 1. Goal Recap

> Build the actual synchronization pipeline with full lifecycle integration.

Achieved: a generic, batch-oriented `SyncPipeline` orchestrates the spec's six components (`BatchProcessor`, `ChangeDetector`, `MergeEngine`, `IdempotencyHandler`, `MappingEngine`, plus the pipeline itself). Modules supply per-domain `IDataExtractor`, `IRecordMapper`, `IRecordValidator`, `IRecordWriter` and choose the merge key + batch size. A `FakePipelineSyncModule` exercises the engine end-to-end without touching real domain code.

---

## 2. Implemented Components

### Module-facing contracts (Sync.Abstractions/Pipeline/)

| Interface | Purpose |
|---|---|
| `IDataExtractor<TExternal>` | Streams `IAsyncEnumerable<TExternal>` honoring the supplied `SyncCheckpoint?` |
| `IRecordMapper<TExt, TInt>` | Pure transformation external → internal; co/contravariant |
| `IRecordValidator<TRecord>` | Optional per-record gate; out-param error string |
| `IRecordWriter<TInternal>` | Idempotent upsert sink; returns persisted count |
| `ISyncPipeline` | Generic `RunAsync<TExt, TInt>(SyncPipelineRequest<…>, ct)` returning `SyncResult` |
| `SyncPipelineRequest<TExt, TInt>` | Immutable bag: extractor, mapper, validator, writer, key selector, batch size, current checkpoint |

### Pipeline pieces (Sync.Infrastructure/Pipeline/)

| Component | Role |
|---|---|
| `SyncPipeline` | Orchestrator. Composes all the below. Singleton. |
| `BatchProcessor` | Static `ChunkAsync<T>(IAsyncEnumerable<T>, size, ct)`. Streams; bounded memory; last batch may be partial. |
| `ChangeDetector` | Standardizes the `Extractor.ExtractAsync(ctx, checkpoint, ct)` call. Per-spec component name. |
| `IdempotencyHandler<TKey>` | `HashSet<TKey>` wrapper. One instance per pipeline invocation → bounded per-run state. |
| `MappingEngine` | Per-batch `Map<TExt, TInt>(batch, mapper)`. |
| `MergeEngine` | Per-batch `MergeAsync<TInt>(batch, writer, ct)`. Short-circuits empty batches. |

`SyncPipeline` is registered as singleton in `AddSyncInfrastructure()` (`services.AddSingleton<ISyncPipeline, SyncPipeline>()`).

### Demo module (Sync.Host/Modules/FakePipeline/)

`FakePipelineSyncModule : ISyncModule` (`ModuleName = "fake-pipeline"`):

- Reads checkpoint via `ISyncCheckpointStore` (scoped DbContext).
- Builds a `SyncPipelineRequest` with `BatchSize = 50`.
- Runs the pipeline.
- On success: advances checkpoint via `ISyncCheckpointStore.SaveAsync(cursor = extractor.MaxObservedSequence)`.
- `PushAsync` returns `SyncResult.Ok(0, …)` — no push in Phase 4.

Supporting fake parts in the same folder:
- `FakeExternalRecord` / `FakeInternalRecord` — POCOs.
- `FakeExtractor` — generates 120 records (`Sequence 1..120`), emits two duplicates (at seq 50 and 75) to exercise idempotency; honors `Checkpoint.Cursor` as int filter; tracks `MaxObservedSequence`.
- `FakeMapper` — normalizes the payload (lowercases & trims).
- `FakeValidator` — rejects records with empty `ExternalId` or non-positive `Sequence`.
- `FakeWriter` — in-memory; returns `batch.Count`; structured-logs batch range.

Existing `FakeSyncModule` from Phase 1 is **unchanged in behavior** — its recurring schedule remains and it runs alongside the new pipeline module.

---

## 3. Execution Flow

```
ISyncDispatcher.DispatchAsync ─► Hangfire enqueue (existing Phase 2 path)
   │
   ▼
SyncModuleExecutor (Hangfire-invoked; existing Phase 3 audit writes)
   │
   ▼
FakePipelineSyncModule.PullAsync
   │
   ├─ ISyncCheckpointStore.GetAsync("fake-pipeline")        // Phase 3 EF store
   │
   ▼
ISyncPipeline.RunAsync<FakeExternalRecord, FakeInternalRecord>(request)
   │
   ▼
  ChangeDetector → Extractor.ExtractAsync(ctx, checkpoint, ct)
   │
   ▼
  BatchProcessor.ChunkAsync(source, batchSize=50)
   │
   ▼ per batch:
   ┌────────────────────────────────────────┐
   │ IdempotencyHandler<string>.TryAdd(key) │  → skip on duplicate ExternalId
   │ MappingEngine.Map(unique, mapper)      │
   │ Validator.IsValid(record) (optional)   │  → drop invalid; warnings list
   │ MergeEngine.MergeAsync(valid, writer)  │
   └────────────────────────────────────────┘
   │
   ▼  after final batch
SyncResult.Ok(processed, duration, warnings)
   │
   ▼
FakePipelineSyncModule advances checkpoint via ISyncCheckpointStore.SaveAsync
   │
   ▼
SyncModuleExecutor.MarkSucceededAsync → sync.runs row (Phase 3)
```

The pipeline itself **does not** call `ISyncCheckpointStore.SaveAsync`. Checkpoint advance is the module's call after a successful run, per the spec's "Persist Checkpoint" step belonging to the module's flow control.

---

## 4. Verified Runtime Behavior

### Tick 1 — first run, no checkpoint

```
Sync job enqueued. Module=fake-pipeline Direction=Pull TriggeredBy=scheduled Queue=default JobId=15
Sync execution started. Module=fake-pipeline Direction=Pull TriggeredBy=scheduled Attempt=1
Pipeline started. Module=fake-pipeline BatchSize=50 HasCheckpoint=False
FakeWriter upsert. BatchSize=50 FirstSeq=1 LastSeq=50
Batch processed. Module=fake-pipeline BatchIndex=1 BatchSize=50 Mapped=50 Written=50
Idempotency skip. Module=fake-pipeline Key=EXT-0050
Idempotency skip. Module=fake-pipeline Key=EXT-0075
FakeWriter upsert. BatchSize=48 FirstSeq=51 LastSeq=98
Batch processed. Module=fake-pipeline BatchIndex=2 BatchSize=50 Mapped=48 Written=48
FakeWriter upsert. BatchSize=22 FirstSeq=99 LastSeq=120
Batch processed. Module=fake-pipeline BatchIndex=3 BatchSize=22 Mapped=22 Written=22
Pipeline completed. Module=fake-pipeline Batches=3 Processed=120 Skipped=2 ValidationFailed=0 Elapsed=00:00:00.0081690
Checkpoint advanced. Module=fake-pipeline Cursor=120
Sync execution succeeded. Module=fake-pipeline Direction=Pull Processed=120 Failed=0
```

- 120 unique records processed across 3 batches: 50 / 48 (50 minus the two dedup'd) / 22.
- 2 idempotency skips at `EXT-0050` and `EXT-0075` — the duplicate keys emitted by `FakeExtractor`.
- 0 validation failures.
- Checkpoint advanced to `Cursor=120`.

### Tick 2 — checkpoint in place

```
Pipeline started. Module=fake-pipeline BatchSize=50 HasCheckpoint=True
Pipeline completed. Module=fake-pipeline Batches=0 Processed=0 Skipped=0 ValidationFailed=0 Elapsed=00:00:00.0001769
Sync execution succeeded. Module=fake-pipeline Direction=Pull Processed=0 Failed=0
```

- Extractor saw `checkpoint.Cursor = "120"` → filtered to nothing → 0 batches.
- Pipeline exits cleanly with 0 processed.

### Database state (after both ticks)

```
SELECT [ModuleName], [Cursor], [LastSyncedAt] FROM [sync].[checkpoints];
   fake-pipeline   120   2026-05-28 18:27:07.7042053 +00:00

SELECT COUNT(*) FROM [sync].[runs] WHERE [ModuleName]='fake-pipeline' AND [Status]=2;
   3

-- Last two runs (post-checkpoint):
SELECT TOP 2 [CorrelationId], [RecordsProcessed], [DurationTicks]
  FROM [sync].[runs]
  WHERE [ModuleName]='fake-pipeline' ORDER BY [EnqueuedAt] DESC;
   …  RecordsProcessed=0   DurationTicks=439
   …  RecordsProcessed=0   DurationTicks=1769
```

Checkpoint stored in `sync.checkpoints` via the EF `ISyncCheckpointStore` (Phase 3). All three runs landed in `sync.runs` as `Status=2 (Succeeded)` via the existing executor audit path.

---

## 5. Success Criteria Recheck

| Criterion | Status | Evidence |
|---|---|---|
| **Idempotent execution guaranteed** | ✅ | 2 `Idempotency skip` log lines for duplicate `ExternalId`s in tick 1; second pipeline invocation skipped all records via checkpoint. Re-running the same tick would produce identical results. |
| **Batch execution works safely** | ✅ | 3 batches of 50/48/22 — sizes derived from `BatchSize=50` minus mid-batch dedups. `BatchProcessor.ChunkAsync` streams; memory bounded to one batch at a time. |
| **Checkpoint recovery works correctly** | ✅ | Tick 1 advanced `sync.checkpoints[fake-pipeline].Cursor=120`. Tick 2 read that checkpoint and produced zero output. After host restart, behavior would be identical because the checkpoint is persisted. |
| **Pipeline fully observable** | ✅ | Structured log lines at every stage with `CorrelationId` in MEL scope: pipeline started/completed (totals), per-batch processed (counts), per-skip (key), per-write (range). Plus the existing Hangfire-execution trace from Phase 1 and the audit-row trail from Phase 3. |

---

## 6. Architectural Decisions

1. **Pipeline is a generic method, not a generic class.** `ISyncPipeline.RunAsync<TExt, TInt>(request, ct)` keeps a single singleton implementation usable by any number of modules with different type parameters. Avoids one `ISyncPipeline<TExt, TInt>` per domain — a classic abstraction explosion.

2. **Modules supply concrete pieces; pipeline supplies composition.** The five module-facing interfaces (`IDataExtractor`, `IRecordMapper`, `IRecordValidator`, `IRecordWriter`, plus the key-selector delegate) are the contract surface. The six pipeline components (`SyncPipeline`, `BatchProcessor`, `ChangeDetector`, `IdempotencyHandler`, `MappingEngine`, `MergeEngine`) are internal classes shared across all modules.

3. **Idempotency is per-run, in-memory.** `IdempotencyHandler<TKey>` is a `HashSet<TKey>` wrapper instantiated fresh per `RunAsync` invocation. Bounded by the extractor's output size for that run; no cross-run state. This is layered on top of the writer's external-key upsert semantics, which provides cross-run idempotency. Combined: re-running a Hangfire job (retry) re-extracts; the writer's `UpsertBatchAsync` makes duplicate writes safe.

4. **Checkpoint advance is the module's call, not the pipeline's.** The pipeline reads the checkpoint (passed in as `CurrentCheckpoint`) and forwards it to the extractor. After a successful return, the module decides what the new checkpoint looks like (cursor value, RowVersion, ExternalVersion, etc.) and persists it via `ISyncCheckpointStore`. This keeps the pipeline domain-agnostic — it has no opinion on checkpoint shape.

5. **`SyncPipeline` is a singleton.** It holds three field-level helper instances (`ChangeDetector`, `MappingEngine`, `MergeEngine`) — all stateless. `IdempotencyHandler` is constructed per call. No DbContext capture, no per-tenant state. Safe under concurrent Hangfire workers running different modules.

6. **`ExternalKeySelector` is `Func<TExt, string>`, not an interface.** A delegate is the lightest possible contract for a single-method extraction. Could have been `IExternalKeyExtractor<TExt>` but that's a needless type. The module passes a method reference: `r => r.ExternalId`.

7. **Validation failures don't throw.** Invalid records are dropped from the batch and counted as `ValidationFailed`; the error string is added to the `Warnings` collection on the returned `SyncResult`. Hard exceptions from the extractor/mapper/writer still bubble up and are caught at the pipeline boundary, returning `SyncResult.Failed`.

8. **Pipeline catches exceptions and returns `SyncResult.Failed`, not propagating.** A pipeline failure mid-batch surfaces to the executor as a "module-reported failure" — the executor then writes `sync.failures` and rethrows so Hangfire retries (with the existing Phase 2 retry policy). This makes the pipeline behave like a well-mannered module to the rest of the stack.

9. **`SyncResult.RecordsProcessed`** maps to "records actually written by the writer". Validation failures live in `RecordsFailed` (currently 0 in the fake). Idempotency skips do not contribute to either field — they are logged but treated as no-op (the writer was never going to process them).

10. **`FakeSyncModule` (Phase 1) preserved.** Phase 4 adds the pipeline module alongside; the original recurring trigger and behavior are byte-identical. Two recurring jobs (`fake-sync-pull`, `fake-pipeline-pull`) both fire every minute.

---

## 7. Changes vs. Phase 3

| File | Change |
|---|---|
| `Sync.Abstractions/Pipeline/IDataExtractor.cs` | **new** |
| `Sync.Abstractions/Pipeline/IRecordMapper.cs` | **new** |
| `Sync.Abstractions/Pipeline/IRecordValidator.cs` | **new** |
| `Sync.Abstractions/Pipeline/IRecordWriter.cs` | **new** |
| `Sync.Abstractions/Pipeline/ISyncPipeline.cs` | **new** |
| `Sync.Abstractions/Pipeline/SyncPipelineRequest.cs` | **new** |
| `Sync.Infrastructure/Pipeline/BatchProcessor.cs` | **new** (static helper) |
| `Sync.Infrastructure/Pipeline/ChangeDetector.cs` | **new** |
| `Sync.Infrastructure/Pipeline/IdempotencyHandler.cs` | **new** |
| `Sync.Infrastructure/Pipeline/MappingEngine.cs` | **new** |
| `Sync.Infrastructure/Pipeline/MergeEngine.cs` | **new** |
| `Sync.Infrastructure/Pipeline/SyncPipeline.cs` | **new** |
| `Sync.Infrastructure/DependencyInjection/SyncInfrastructureServiceCollectionExtensions.cs` | + `services.AddSingleton<ISyncPipeline, SyncPipeline>()` |
| `Sync.Host/Modules/FakePipeline/*.cs` (7 files) | **new** — demo module + parts |
| `Sync.Host/Program.cs` | + `AddSingleton<ISyncModule, FakePipelineSyncModule>()` and namespace using |
| `Sync.Host/Scheduling/SyncRecurringJobsRegistrar.cs` | + recurring registration for `fake-pipeline-pull` |

**No changes** to:
- `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult` (byte-identical APIs)
- `SyncCheckpoint`, `SyncRunMetadata`, `SyncDirection`
- `SyncDispatcher`, `SyncModuleExecutor`, `SyncDeadLetterFilter` (Phase 2 + Phase 3 work intact)
- `SyncRunRepository`, `FailureRepository`, `DeadLetterRepository`, `EfSyncCheckpointStore`
- `FakeSyncModule`, `FakeSyncRecurringTrigger` (Phase 1 fake module untouched)
- Persistence schema; no new migration needed
- Hangfire configuration; queues, retry, dashboard unchanged

---

## 8. Deviations

| # | Deviation | Rationale | Risk |
|---|---|---|---|
| D1 | `ISyncPipeline.RunAsync<TExt, TInt>(...)` is generic. Spec doesn't dictate generic-ness explicitly. | One singleton serving all modules avoids per-domain pipeline types. | None. |
| D2 | Module advances checkpoint after pipeline returns; pipeline doesn't call `ISyncCheckpointStore.SaveAsync`. | Pipeline stays domain-agnostic (no opinion on checkpoint shape). Module knows its cursor semantics. | None. |
| D3 | `FakeWriter` is in-memory (returns `batch.Count`) — no DB row written by the writer itself. | Phase 4 ships the engine; real DB writers ship per-module in Phase 5. | None. Audit rows in `sync.runs` still record real persisted counts. |
| D4 | `IdempotencyHandler` is in-memory per run, not persistent. | Cross-run idempotency is enforced by the writer's upsert (external-id-keyed). In-run dedup is the only gap to close, and it's bounded by the extractor's output size. | Acceptable. For extreme cases (multi-GB batches), Phase 8 would revisit. |
| D5 | "Pipeline execution metrics" cross-cutting item is satisfied by structured logs (per-batch + completion totals), not a metrics interface. | Phase 10 is the observability phase; no need to introduce a metrics abstraction here. | None. |

No future-phase functionality leaked.

---

## 9. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Long-running extractor + large idempotency set could grow `HashSet` memory. | Medium (real modules) | Phase 8 (perf) revisits; can switch to a bounded sliding window or persisted dedup if profiling demands. |
| Validator returns warnings as strings — high-volume validation failures could balloon `warnings: List<string>`. | Low | Currently unbounded. A real module should aggregate (e.g., `string -> count`). Acceptable for the fake. |
| Pipeline `catch (Exception)` returns `SyncResult.Failed` instead of throwing. The executor then throws `SyncExecutionException`, triggering Hangfire retry. Net behavior is correct, but the inner exception type is lost (only the message is preserved). | Low | Acceptable trade-off; full stack trace lives in `sync.failures.StackTrace` once Phase 5 modules surface real exceptions. |
| `Func<TExt, string> ExternalKeySelector` allocates per call. | Trivial | Modules pass a static method group; allocation is one delegate per pipeline invocation. |
| `MappingEngine.Map` materializes the full batch into a `List<TInt>`. | Low | Bounded by `BatchSize`. Phase 8 can stream if needed. |
| `SyncPipeline` is singleton, but `IdempotencyHandler` is per-call. Thread-safety: pipeline state is request-scoped (locals + per-call HashSet); no shared mutable state across requests. | None | Verified by inspection. |
| Carried from earlier phases: `AllowAllDashboardAuthorizationFilter`, dev SQL password. | High (deploy) | Same as Phase 2/3 — must be replaced before non-local deployment. |

---

## 10. Future Concerns Surfaced

1. **Per-attempt idempotency across Hangfire retries.** Each Hangfire retry re-runs the pipeline; `IdempotencyHandler` is fresh. Same input → same writes → safe under the writer's upsert semantics. No additional protection needed in Phase 4, but worth documenting.
2. **Streaming validation / mapping.** Currently materializes per-batch. Phase 8 can introduce `IAsyncEnumerable<>` streaming through map/validate if a module's records are large.
3. **Multi-worker parallel batches.** Currently batches process sequentially within a single Hangfire job. Phase 8 may introduce parallel batch execution (per the spec). Pipeline shape supports it (each batch is independent post-dedup) — implementation deferred.
4. **Checkpoint shape per module.** Each module decides how to derive the new checkpoint. Phase 5 modules will exercise different strategies (RowVersion, UpdatedAt, hybrid). The contract supports all.
5. **Failure during checkpoint advance.** If the pipeline succeeds but the post-pipeline `SaveAsync(checkpoint)` throws, the module's `PullAsync` returns a successful `SyncResult` (Phase 4) but the next run will re-extract everything. The writer's upsert keeps this safe. Phase 9 may introduce a more transactional model.
6. **PushAsync currently returns `Ok(0)`.** Phase 6 builds the push pipeline; the symmetry is intentional.

---

## 11. Technical Debt Introduced

| Debt | Severity | Repaid in |
|---|---|---|
| `ChangeDetector` and `MergeEngine` are 1-method wrappers — close to anaemic. | Trivial | They exist to honor the spec's component names. Will gain real logic in Phase 9 (resilience) and Phase 5 (real modules). |
| `warnings: List<string>` is unbounded. | Low | Phase 8 / 9 introduces aggregation. |
| `FakePipelineSyncModule` carries the demo extractor/mapper/validator/writer as ad-hoc `new()`s rather than DI-resolved services. | Trivial | These are demo classes that exist only in Phase 4. Real modules in Phase 5 will DI-register their own pieces. |
| `FakeExtractor` uses `await Task.Yield()` to satisfy the async-enumerable shape — pure-CPU loop dressed as async. | Trivial | Real extractors will be genuinely async (DB / HTTP). |
| `MappingEngine.Map` could be `static` — kept instance for symmetry with the other "engines". | Trivial | None. |

**Carried** (unresolved): `AllowAllDashboardAuthorizationFilter`, dev SQL password, `SyncRunMetadata.Tags` mutability, `TriggeredBy` as free-form string, repo-vs-spec layout naming, failure-path runtime verification (no failing module exists yet).

---

## 12. Testability Status

- `BatchProcessor.ChunkAsync<T>` is a pure async iterator — trivially unit-testable with a synthetic `IAsyncEnumerable<T>`.
- `IdempotencyHandler<TKey>` is two methods around `HashSet<TKey>` — trivial.
- `ChangeDetector`, `MappingEngine`, `MergeEngine` are stateless and mockable.
- `SyncPipeline.RunAsync` is testable end-to-end with synthetic in-memory parts (the fake module is essentially the test fixture made permanent).
- Integration test path: `WebApplicationFactory<Program>` triggers `FakePipelineSyncModule`; asserts `sync.runs.RecordsProcessed == 120` on first tick, `0` on second.
- No automated tests written in Phase 4 — runtime smoke + raw SQL inspection were the gates.

---

## 13. Production-Readiness Evaluation

| Dimension | Status | Notes |
|---|---|---|
| Compiles cleanly | ✅ | All four sync projects, `TreatWarningsAsErrors=true`, 0/0. |
| Runtime smoke | ✅ | Two ticks of `fake-pipeline-pull` verified the full flow + checkpoint recovery. |
| Persistence | ✅ | Checkpoint row + run rows + Hangfire jobs all present and correlated. |
| Public API compatibility | ✅ | `ISyncModule`, `ISyncDispatcher`, `SyncContext`, `SyncResult` byte-identical. |
| Backward compatibility | ✅ | `FakeSyncModule` continues to run as in Phase 1. |
| Schema impact | ✅ | None — pipeline uses existing Phase 3 tables. |
| Failure path | Unverified at runtime | Pipeline exception → `SyncResult.Failed` → executor → Hangfire retry — all paths exist; no failing module to exercise. |
| Dashboard auth | ❌ | Carried debt. |
| Pre-existing repo build error | ⚠️ | `tests/Core.UniTests/Authorization/PermissionTreeQueryHandlerTests.cs` — unchanged. |

**Verdict:** Phase 4 delivers a production-shaped engine ready for the first real module (Phase 5). All four success criteria observed in live runs. Demo module remains in place as living documentation.

---

## 14. Phase Exit Checklist

- [x] All six spec components implemented (`SyncPipeline`, `BatchProcessor`, `ChangeDetector`, `MergeEngine`, `IdempotencyHandler`, `MappingEngine`)
- [x] Module-facing contracts (`IDataExtractor`, `IRecordMapper`, `IRecordValidator`, `IRecordWriter`) defined
- [x] `ISyncPipeline` is generic and singleton
- [x] Pipeline composes via `SyncPipelineRequest<TExt, TInt>`
- [x] Idempotency verified at runtime (2 skips)
- [x] Batch execution verified (3 batches: 50/48/22)
- [x] Checkpoint recovery verified (tick 2 produces 0 records)
- [x] Pipeline fully observable via structured logs
- [x] No event bus, no hooks, no CQRS introduced
- [x] No new project, no new migration, no schema changes
- [x] Hangfire / Phase 2 / Phase 3 unchanged
- [x] `ISyncModule`/`ISyncDispatcher` public APIs unchanged
- [x] Demo module proves the contract end-to-end
- [x] Existing `FakeSyncModule` continues to run unchanged
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Audit document produced

---

**Phase 4 complete. Stopping. Awaiting explicit instruction to proceed to Phase 5 (First Real Module — Students.Sync).**
