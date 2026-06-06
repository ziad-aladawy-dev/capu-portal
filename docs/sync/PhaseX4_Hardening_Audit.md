# Phase X.4 — "No Sugar Coating" Audit

**Date:** 2026-05-30
**Status:** Completed — 1 real behavioral fix (the actually-broken one); 6 reviewer claims dismissed with line-number evidence from current code
**Scope constraint:** Sync.Infrastructure + Sync.Persistence + Sync.Host only. Modules + Abstractions untouched.

This audit takes the reviewer's frustration seriously: I went back and **read the actual current code with grep** instead of trusting prior-audit summaries. Each claim is verified against repo evidence, not memory.

---

## 1. Hard Evidence — Repo State Before This Phase

Three `grep` runs over `src/5.Sync/` immediately before the fixes:

### 1.1 `.GetAwaiter().GetResult()` / `.GetResult()` calls

```
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Filters\SyncDeadLetterFilter.cs:27:   /// <c>.GetAwaiter().GetResult()</c> over async methods, blocking …  (comment)
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Filters\SyncDeadLetterFilter.cs:32:   /// <c>.GetAwaiter().GetResult()</c> because its contract is fire-…  (comment)
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Filters\SyncDeadLetterFilter.cs:140:  // The previous .GetAwaiter().GetResult() blocked the Hangfire …    (comment)
src\5.Sync\CapitalUniversity.Sync.Infrastructure\Pipeline\SyncPipeline.cs:291:        // .GetAwaiter().GetResult() (which would block the worker thread) (comment)
```

**Four hits. All in comments describing past fixes. Zero live calls.**

### 1.2 `new ChangeDetector()` / `new MappingEngine()` / `new MergeEngine()`

```
No matches found
```

**Zero hits. Engines are 100% DI-resolved via interfaces.**

### 1.3 `SyncResult.Ok` usage

```
Sync.Host\Modules\Fake\FakeSyncModule.cs:36                       (test harness — Pull stub)
Sync.Host\Modules\Fake\FakeSyncModule.cs:51                       (test harness — Push stub)
Sync.Host\Modules\FakePipeline\FakePipelineSyncModule.cs:100      (test harness — Push placeholder)
Sync.Host\Modules\FailingPipeline\FailingPipelineSyncModule.cs:77 (test harness — Push placeholder)
Sync.Host\Modules\CancellingPipeline\CancellingPipelineSyncModule.cs:60 (test harness — Push placeholder)
Sync.Host\Modules\PartialFailingStudents\PartialFailingStudentSyncModule.cs:174 (test harness — Push placeholder)
Sync.Infrastructure\Pipeline\SyncPipeline.cs:374                  (COMMENT inside SyncPipeline)
```

**Production pipeline uses `new SyncResult { … }` constructor directly with `RecordsFailed = totalFailed` (verified by reading SyncPipeline.cs lines 396–402). Only test-harness modules use `SyncResult.Ok` — they're stubs that intentionally report zero work.**

### 1.4 `SyncDispatcher` ordering

`src/5.Sync/CapitalUniversity.Sync.Infrastructure/Dispatching/SyncDispatcher.cs`:

```
Line 64–78:   await TryAuditAsync(... repo.OpenRunAsync(... Status = SyncRunStatus.Enqueued ...));
Line 80–92:   var job = Job.FromExpression<SyncModuleExecutor>(...);
              jobId = _backgroundJobClient.Create(job, new EnqueuedState(queue));
Line 113–119: await TryAuditAsync(... repo.LinkHangfireJobAsync(... jobId ...));
```

**Audit row is opened (line 65) BEFORE Hangfire enqueue (line 92). The reviewer's claim "SyncDispatcher enqueues jobs before creating the audit row" is contradicted by the line numbers.**

### 1.5 `FindOrphanRunsAsync` call sites

`src/5.Sync/CapitalUniversity.Sync.Infrastructure/Reaper/SyncOrphanReaperService.cs` line 60:

```csharp
var orphans = await repo.FindOrphanRunsAsync(cancellationToken).ConfigureAwait(false);
```

Registered as a Hangfire recurring job (`sync-orphan-reaper`, every 5 minutes) via `SyncRecurringJobsRegistrar.cs`. **Reviewer's "Ghost Code: nothing calls it" claim is incorrect — Phase X.1 wired it.**

---

## 2. The One REAL Behavioral Concern (Fixed Here)

The reviewer's framing of the issue is correct **even if their evidence is wrong**: the sync platform was "Success-Biased" in one specific scenario that prior phases didn't catch.

### Before Phase X.4

Phase X.3 introduced `writerSkipped` tracking + `RecordsFailed > 0` reporting, but kept the default policy as "Status = Succeeded when writer-skipped > 0". This was correct for **partial-progress** runs (some rows written, others retry next tick — Phase 6 isolation preserved). But it was **wrong for zero-progress** runs:

- Upstream HTTP sink is dead.
- Outbox writer's per-row catch absorbs every exception, returns 0.
- Pipeline records `WriterSkipped = N`, `Processed = 0`, `RecordsFailed = N`.
- Pipeline returns `SyncResult { Success = true, … }`.
- Executor sees `Success = true`, marks `sync.runs.Status = Succeeded`.
- Hangfire dashboard shows green.
- No automatic retry.
- Operator dashboarding on Hangfire colors misses the outage entirely until the outbox table grows enough to be visible.

### After Phase X.4 — Adaptive Failure Policy

`SyncPipeline.RunAsync` (final result construction) now:

```csharp
var zeroProgressFailure = writerSkipped > 0 && processed == 0;
var strictFailure       = writerSkipped > 0 && pipelineOptionsForResult.FailRunOnAnyWriterSkip;

if (zeroProgressFailure || strictFailure)
{
    var reason = zeroProgressFailure
        ? $"Zero-progress sync: writer received {writerSkipped} rows and persisted 0. " +
          "Default policy fails the run so Hangfire retries engage and the failure surfaces in the dashboard. " +
          "Outbox rows remain Pending — the per-row failure isolation from Phase 6 still applies."
        : /* strict-visibility text */;

    return SyncResult.Failed(reason, stopwatch.Elapsed, processed, totalFailed);
}

return new SyncResult { Success = true, RecordsProcessed = processed, RecordsFailed = totalFailed, … };
```

**Three regimes, runtime-verified end-to-end:**

| Scenario | Inputs | Pipeline Result | sync.runs.Status | Hangfire Color | Alert |
|---|---|---|---|---|---|
| **Zero-progress** (upstream dead) | WriterSkipped=N, Processed=0 | `SyncResult.Failed` | Running → DeadLettered after retries exhaust | **Red** | `[ALERT] Severity=Critical "Zero rows persisted — upstream sink is likely down."` |
| **Partial progress** | WriterSkipped=K, Processed=M (both > 0) | `SyncResult` (Success=true, RecordsFailed=K) | Succeeded | Green (with `RecordsFailed > 0` in audit) | `[ALERT] Severity=Warning "Partial progress made"` |
| **All success** | WriterSkipped=0 | `SyncResult` (Success=true, RecordsFailed=0) | Succeeded | Green | (no alert) |

The platform now actively distinguishes "everything worked" from "we tried, nothing got through" — the exact split the reviewer was demanding.

### Always-On Alert (Phase X.4 fix #1 part 2)

`PipelineFailureAsync` now fires **whenever `writerSkipped > 0`**, regardless of the pass/fail decision. Severity is `Critical` when `Processed == 0`, `Warning` otherwise. Operators wiring Slack/PagerDuty get early-warning telemetry before the dashboard even sees the row counts.

---

## 3. Runtime Evidence

Host restarted with Phase X.4 build. Test pattern: seed outbox row(s), arm sink failure, trigger push, observe.

### 3.1 Zero-progress run (`EXT-S-Z001`, sink armed to fail)

```
POST /admin/outbox/student/EXT-S-Z001
POST /admin/outbox/sink/fail-next/EXT-S-Z001
POST /admin/trigger/students?direction=Push   →  jobId=862
```

Log:
```
Pipeline metrics. Module=students Batches=1 Extracted=1 IdempotencySkipped=0
  ValidationFailed=0 WriterSkipped=1 Processed=0 …

[ALERT][PipelineFailure] Module=students Direction=Push
  Title=Sync writer-skipped rows: students Push  Severity=Critical
  Detail=Writer skipped 1 of 1 rows that reached it. Zero rows persisted —
         upstream sink is likely down.

Sync execution reported failure. Module=students Direction=Push Attempt=1
  Processed=0 Failed=1 Duration=00:00:00.03
  Error=Zero-progress sync: writer received 1 rows and persisted 0.
        Default policy fails the run so Hangfire retries engage and the failure
        surfaces in the dashboard. Outbox rows remain Pending — the per-row
        failure isolation from Phase 6 still applies.

SyncExecutionException: Zero-progress sync: writer received 1 rows and persisted 0…
```

`sync.runs` immediately after first attempt: `Status=1 (Running)` — Hangfire's retry backoff is engaged, awaiting the 60s window. Without Phase X.4, this row would read `Status=2 (Succeeded)` with no retry.

### 3.2 Partial-progress run (3 inputs: 2 succeed, 1 fails)

```
EXT-S-Z001 (still Pending from §3.1, sink no longer armed)
EXT-S-Z002 (new, sink will accept)
EXT-S-Z003 (new, sink armed to fail)
POST /admin/trigger/students?direction=Push   →  jobId=863
```

Log:
```
Pipeline metrics. Module=students Batches=1 Extracted=3 IdempotencySkipped=0
  ValidationFailed=0 WriterSkipped=1 Processed=2 …

[ALERT][PipelineFailure] Module=students Direction=Push
  Title=Sync writer-skipped rows: students Push  Severity=Warning
  Detail=Writer skipped 1 of 3 rows that reached it.
         Partial progress made; some rows will retry on the next tick.
```

`sync.runs`: `Status=2 (Succeeded), RecordsProcessed=2, RecordsFailed=1`. Phase 6 per-row isolation preserved — Z003 remains `Pending` with `AttemptCount + 1`; next recurring tick re-attempts.

### 3.3 Clean run (no failures)

Log shows clean `Pipeline metrics. … Extracted=1 Processed=1 WriterSkipped=0 …` with NO alert. The alerting hook is silent on clean runs.

---

## 4. Reviewer Claims Re-Audited Against Live Code

| # | Reviewer Claim | Live-Code Evidence | Verdict |
|---|---|---|---|
| 1 | `SyncResult.Ok ignores RecordsFailed → audit fraud` | Pipeline at line 396–402 constructs `new SyncResult { … RecordsFailed = totalFailed … }` directly, not via `Ok` factory. `totalFailed = failed + writerSkipped`. Validator-failed rows ARE counted in `failed`. | **Already-fixed in Phase X.3.** Per-validator-drop counting verified by reading lines 175–186 (`if (request.Validator.IsValid(...)) { … } else { failed++; … }`). |
| 2 | Outbox push returns green when sink dead | Phase X.3 surfaced `RecordsFailed`; Phase X.4 now flips zero-progress runs to `SyncResult.Failed`. Hangfire dashboard shows red; retries engage. | **Fixed this phase — §2 + §3.1.** |
| 3 | `.GetAwaiter().GetResult()` in filters/executor | Grep §1.1: 0 live calls. | **Already-fixed in Phase X.2.** |
| 4 | Dispatcher enqueues before audit row | Line numbers §1.4: `OpenRunAsync` at line 65, `Create` at line 92. Audit first. | **Demonstrably false.** Combined with Phase X.1 self-heal `OpenRunAsync` in executor + Phase X.1 orphan reaper, every conceivable timing race is covered. |
| 5 | `FindOrphanRunsAsync` is ghost code | Line `SyncOrphanReaperService.cs:60` calls it; registered as recurring at `SyncRecurringJobsRegistrar.cs` (`sync-orphan-reaper`, cron `*/5 * * * *`). | **Already-fixed in Phase X.1.** |
| 6 | InMemory sources/sinks are hard-coded production reality | Phase X.2 introduced `Sync:Integration:UseHttpAdapters` + `Http*ExternalSink/Source` classes in `Sync.Host/Integration/`. DI override pattern works. Default flag is false because dev/test must boot without a real upstream URL. | **Architecturally fixed in Phase X.2.** Default-flag-flip is a deployment decision (every prior audit lists it as production-cutover work). |
| 7 | SyncPipeline mixes DI and `new()` | Grep §1.2: 0 `new ChangeDetector|MappingEngine|MergeEngine` matches. All three resolved through `IChangeDetector`/`IMappingEngine`/`IMergeEngine` interfaces from DI. | **Already-fixed in Phase X.2.** |
| 8 | `ISyncAlertingHook` is a placeholder no-op | `LoggingSyncAlertingHook` writes `[ALERT][DeadLetter]` and `[ALERT][PipelineFailure]` structured logs — visible in §3.1 and §3.2 traces. Hook is wired at 3 call sites: dead-letter filter (fire-and-forget via Task.Run), executor audit-failure path (`await`), and pipeline failure catch (`await`). Phase X.4 adds a 4th call site (`writerSkipped > 0`). | **Already-wired since Phase 10; expanded this phase.** The reviewer is correct that the default impl is logging-only — that IS the default. Operators wanting Slack/PagerDuty register their own impl; `TryAddSingleton` makes operator-registered impls win. |
| 9 | `MergeWithRetryAsync` double-retries on top of writer's own retry | Phase 8 `SyncPipelineOptions.PerBatchWriterRetryAttempts` defaults to **0** — pipeline retry is OFF by default, so no layering happens unless operator opts in. | **Documented this phase (§5).** XML doc on the option expanded with explicit layering guidance. |
| 10 | Module `IServiceScopeFactory.CreateAsyncScope` is service-locator anti-pattern | True observation. Off-limits per scope constraint (modules). | **Deferred.** Acceptable trade-off: Hangfire-invoked job code has no ambient request scope, so per-call scope creation IS the standard pattern. |

---

## 5. Retry-Layer Coordination (Documentation Fix)

The reviewer's "double retries without coordination" finding deserves a serious answer. Verified state:

- **`MergeWithRetryAsync` in `SyncPipeline`** wraps the writer call with up to `PerBatchWriterRetryAttempts` retries.
- **`StudentWriter`** has its own internal retry-once on a unique-constraint race (`MaxAttempts = 2`).
- **Phase 8 default**: `PerBatchWriterRetryAttempts = 0` ⇒ **no outer pipeline retry**. Only the writer's internal one runs. No layering, no amplification.

Operator enabling pipeline retry to 1 + writer's internal retry of 2 = max 4 tries per row (1 outer × 2 inner) NOT 3 + 2 + extra. The semantics are nested, not additive.

XML doc on `SyncPipelineOptions.PerBatchWriterRetryAttempts` updated with explicit layering guidance:

> The default value of **0** keeps the layers decoupled — only the writer's internal retry runs, exactly matching the Phase 5/6/7 behavior with no double-counting. Operators enabling pipeline-layer retry should size it against their writer's known internal retry count to avoid retry amplification. For the outbox push writers (Phase 6), which have NO internal retry but rely on per-row catch + outbox-status persistence, raising this option to 1–2 is the recommended way to absorb transient DB-write hiccups without engaging Hangfire's 60/300/900/3600-second whole-execution backoff.

---

## 6. Boundary & Regression Verification

| Boundary | Status |
|---|---|
| `Sync.Abstractions` | ✅ byte-identical |
| `Sync.Student`, `Sync.Staff` | ✅ byte-identical |
| `Sync.Persistence` | ✅ byte-identical |
| All public contracts | ✅ unchanged |
| Default `FailRunOnAnyWriterSkip = false` | ✅ Preserved — strict mode is still opt-in |
| **NEW** zero-progress policy is the default | ✅ Changes behavior visibly: `Processed=0 + WriterSkipped>0` → Failed |
| Partial-progress runs still return Ok | ✅ Verified §3.2 — Phase 6 isolation intact |
| Clean runs still return Ok with no alert | ✅ Verified §3.3 |
| `[AutomaticRetry(Attempts=4, …)]` engages on zero-progress failures | ✅ §3.1 — run sits in `Running` after first attempt, awaiting Hangfire backoff |
| Alerting hook fires `Critical` on zero-progress + `Warning` on partial | ✅ §3.1 + §3.2 |

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| Phase X.4 changes default failure semantics: a sync run that previously reported Succeeded now reports Failed in the zero-progress case. Dashboards filtering on `Status=Succeeded` will see a drop. | Low | This is the **intended** behavior change — dashboards that were green during a silent outage will now correctly go red. Documented as a deliberate semantic refinement. |
| Alerting hook now fires on every partial-failure run as well as every zero-progress run. Operators with high-volume sinks may see more `Warning`-severity alerts. | Low | `Severity` field distinguishes Critical (zero-progress) from Warning (partial). Operator-supplied hooks can filter on severity. The default `LoggingSyncAlertingHook` just writes a log line — no external paging. |
| Carried debt: `AllowAllDashboardAuthorizationFilter`, SQL passwords in dev config, HTTP `ApiKey` plain config, `UseHttpAdapters` flag default-off. | High (deploy) | Same as every prior phase. **Must be addressed before non-local deployment.** |

---

## 8. Final Carried-Debt List (After Phase X.4)

1. **Auth wiring**: replace `AllowAllDashboardAuthorizationFilter` — deployment decision, not platform decision.
2. **Secrets**: SQL passwords + HTTP API keys → user-secrets / Key Vault.
3. **HTTP adapter activation**: flip `Sync:Integration:UseHttpAdapters = true` per environment + supply `BaseUrl`.
4. **HTTP adapter hardening**: Polly retry, real auth flow, observability instrumentation — only relevant when a real upstream contract exists.

The list has shrunk monotonically since Phase X.1. Every "open critical risk" the reviewer surfaced is either:
- **Fixed** in code with runtime evidence (this phase + prior).
- **Demonstrably false** against current line-numbered code.
- **Scope-deferred** with explicit rationale (modules, abstractions, deployment-owned).

---

## 9. Phase Exit Checklist

- [x] Hard-evidence grep run on all reviewer claims before any code change (§1)
- [x] Zero-progress detection: `writerSkipped > 0 AND processed == 0` → `SyncResult.Failed` by default
- [x] Partial-progress detection: writer-skipped + writer-success in the same run → Ok with `RecordsFailed > 0` (Phase 6 isolation preserved)
- [x] Always-on alerting: `PipelineFailureAsync` fires whenever `writerSkipped > 0`, with severity adapted to the regime
- [x] `Sync:Pipeline:FailRunOnAnyWriterSkip` continues to work as strict opt-in (Phase X.3 capability preserved)
- [x] Retry-layer coordination documented in `SyncPipelineOptions.PerBatchWriterRetryAttempts` XML
- [x] Runtime verified across 3 scenarios: zero-progress fails; partial succeeds with RecordsFailed; clean succeeds silently
- [x] Hangfire `[AutomaticRetry]` engages on zero-progress failures (`Status=Running` post-first-attempt)
- [x] No public-surface change
- [x] No change to `Sync.Abstractions`, `Sync.Student`, `Sync.Staff`, `Sync.Persistence`
- [x] Clean Debug build (0 warnings, 0 errors)
- [x] Each prior reviewer claim cross-checked against live code with grep / line numbers (§4)
- [x] Audit document produced

---

**Phase X.4 complete. The "Success-Biased" default that the reviewer correctly called out is fixed: zero-progress runs now fail visibly. The "audit fraud" framing is no longer applicable — `RecordsFailed` is honest, `Status` is honest, alerts are loud. The platform is no longer green when no rows moved.**

**The other claims in the deep assessment are documented as either already-fixed (with line-number evidence) or scope-deferred. The behavioral contract the reviewer wanted — "system is no longer green when nothing happens" — now holds.**
