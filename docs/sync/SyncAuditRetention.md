# Sync Audit Retention Strategy

**Status:** Documentation / preparation only. No retention scheduler is implemented in Phase 5 hardening — this document is the design that a future operational task will execute.

---

## 1. Tables Affected

All under the `sync` schema in `CapitalUniversityDb`.

| Table | Row source | Growth driver |
|---|---|---|
| `sync.runs` | One row per `ISyncDispatcher.DispatchAsync` call | Recurring schedules + ad-hoc admin triggers |
| `sync.jobs` | One row per Hangfire job enqueued by the dispatcher (1:1 with `sync.runs` today; 1:N when Phase 4 batching becomes multi-job) | Same as `sync.runs` |
| `sync.failures` | One row per Hangfire-attempt failure of a module call | Faulty modules + transient external failures |
| `sync.dead_letters` | One row per terminal Hangfire-Failed transition surviving the retry policy | Catastrophic / non-recoverable failures |
| `sync.checkpoints` | One row per module (or per module+direction in future) | Bounded; **not subject to retention** |

`sync_student.students` and other module-owned tables are **not** retention targets — those are the synced business data, not audit.

---

## 2. Expected Growth (order-of-magnitude)

Assuming **5 recurring modules**, each ticking **every minute** (Phase 5 baseline), running **24/7**:

| Table | Rows/day | Rows/month (30d) | Rows/year |
|---|---|---|---|
| `sync.runs` | 5 × 1440 ≈ **7.2k** | ≈ 216k | ≈ 2.6M |
| `sync.jobs` | ≈ same as `sync.runs` | ≈ 216k | ≈ 2.6M |
| `sync.failures` | 0 nominal; ≤ 5 × 5 attempts × 5% failure rate ≈ 1.8k worst case | ≤ 54k | ≤ 650k |
| `sync.dead_letters` | typically 0; bounded by operator-investigation throughput | ≤ thousands | ≤ tens of thousands |

These numbers scale linearly with module count and cron frequency. A practical fleet running 20 modules at minutely cadence would multiply the above by ~4.

Disk footprint per row (approximate, with overhead): `sync.runs` ~200 B, `sync.jobs` ~150 B, `sync.failures` ~1 KB (includes message + stack snippet), `sync.dead_letters` ~1 KB.

→ A year of the Phase-5 baseline footprint is on the order of **1 GB**. Acceptable through the first production year; needs a retention strategy to plateau beyond that.

---

## 3. Recommended Retention Windows

These are **defaults**, not policy mandates — operators should tune to their compliance/postmortem needs.

| Table | Hot window (kept) | Cold archive | Hard delete |
|---|---|---|---|
| `sync.runs` (Status `Succeeded`) | **30 days** | optional cold copy to a "history" table or S3-equivalent | after 90 days |
| `sync.runs` (Status `Failed` / `DeadLettered` / `Cancelled`) | **90 days** | mandatory cold copy | after 365 days |
| `sync.jobs` | tied to parent `sync.runs` | tied to parent | tied to parent |
| `sync.failures` | **90 days** | tied to parent `sync.runs` | tied to parent |
| `sync.dead_letters` | **365 days** (long postmortem window) | mandatory cold copy | after 730 days |
| `sync.checkpoints` | **never deleted** | n/a | n/a |

Successful runs older than 30 days are typically only valuable as anonymous metrics. Failed/dead-lettered rows are kept longer because they're often the input to incident retrospectives.

---

## 4. Cleanup Mechanism (Future Implementation — Not in Phase 5)

When introduced, the cleanup MUST:

1. **Run as a low-priority Hangfire recurring job** — daily, off-peak. No new infrastructure.
2. **Delete in fixed-size batches** (e.g., 5000 rows per `DELETE TOP (N)` loop) to avoid lock escalation on `sync.runs` and `sync.failures`.
3. **Delete children before parents.** Order: `sync.failures` → `sync.dead_letters` → `sync.jobs` → `sync.runs`. (Schema does not enforce FKs today; logical order still matters for queryability during cleanup.)
4. **Respect the windowing above** via configuration (`Sync:AuditRetention:*`).
5. **Honor a kill switch.** A config flag (default off) lets operators disable cleanup if an investigation is in flight.
6. **Emit a single structured summary log per run** with rows-deleted per table and elapsed time. No per-row logging.
7. **Never touch `sync.checkpoints`.** Checkpoints are operational state, not audit.
8. **Never touch the Hangfire schema.** Hangfire owns its own retention via `JobExpirationCheckInterval` / state expiration; that's a separate concern.

Suggested config:

```jsonc
"Sync": {
  "AuditRetention": {
    "Enabled": false,                       // default off until operator opt-in
    "SuccessfulRunsRetentionDays": 30,
    "FailedRunsRetentionDays": 90,
    "FailureRowsRetentionDays": 90,
    "DeadLettersRetentionDays": 365,
    "BatchSize": 5000,
    "RunOnCronExpression": "0 3 * * *"      // daily 03:00 UTC
  }
}
```

---

## 5. What This Document Is Not

- **Not an implementation.** No code, no scheduler, no migrations in Phase 5.
- **Not a hard policy.** Retention windows above are operational defaults; legal/compliance requirements override.
- **Not a Hangfire concern.** Hangfire's `[HangFire].*` schema has its own expiration mechanism configured at storage level.

---

## 6. Triggers for Implementing

Implement when **any** of these is true:

- `sync.runs` exceeds **5 million rows** in production.
- Daily query latency on the dashboard exceeds operator acceptance (typically ~500 ms p99).
- Storage footprint of the `sync` schema exceeds the operator's allocated quota.
- A compliance audit requires automated, documented retention enforcement.

Until then, the documented strategy stands as the agreed plan and the absence of a scheduler is a deliberate, low-risk choice for the Phase-5 maturity level.

---

**Owner:** Sync platform team.
**Next review:** when modules count > 10 OR when `sync.runs` exceeds 1M rows.
