# Modular Bidirectional Sync Service — Implementation Phases (Hangfire Edition)

This document defines an incremental, production-safe implementation plan for the modular synchronization system using **Hangfire** as the background job orchestration engine.

Each phase is self-contained, testable, and designed to avoid architectural instability during development.

This document refers to `Sync_Platform_Model.md`.

---

# 🧱 Phase 0 — Minimal Foundation (Core Contracts Only)

## Goal

Define only the minimum stable contracts required to run a sync engine skeleton.

No speculative abstractions. No eventing. No hooks. No infrastructure dependencies.

This phase exists only to support a working runtime in Phase 1.

---

## 🧩 Core Interfaces

* ISyncModule
* ISyncDispatcher
* ISyncModuleRegistry
* ISyncJobStore
* ISyncCheckpointStore

---

## 🧩 Core Models

* SyncContext
* SyncJobDescriptor
* SyncCheckpoint
* SyncResult
* SyncDirection (Pull / Push)
* SyncRunMetadata

---

## 🧩 Core Errors

* SyncException
* SyncModuleNotFoundException
* SyncExecutionException

---

## 🧩 Logging Contract (Minimal)

* ISyncLogger

### Purpose

* Structured logging with CorrelationId support
* Used only for runtime tracing
* No event system or side effects

---

## ⚠️ Explicitly Deferred (Do NOT implement yet)

These are intentionally postponed until runtime proves real need:

* ISyncEventPublisher
* ISyncEventHandler
* ISyncExecutionHooks
* ISyncNotificationHandler
* ISyncAuthorizationContext

### Reason

These must be derived from real execution behavior, not assumptions.

---

## 📦 Output

* `CapitalUniversity.Sync.Abstractions`
* Contains only core contracts required for execution
* No infrastructure, messaging, or persistence logic

---

## ✅ Success Criteria

* Sync module can be fully described using interfaces
* Dispatcher can resolve and execute a module
* Job descriptor flows end-to-end
* Logging works with correlation tracking
* No eventing, hooks, or messaging exists

---

# 🟡 Phase 1 — Hangfire Host Skeleton (Validated Runtime Loop)

## Goal

Build a fully working runtime engine using Hangfire to execute sync modules end-to-end and expose missing architectural needs through real execution.

This is the first truth-validation phase of the system.

---

## 🧩 Implemented Components

* .NET Worker Service (`Sync.Host`)
* Hangfire Server
* Hangfire Dashboard
* SyncDispatcher
* SyncModuleExecutor
* FakeSyncModule (test module)
* Hangfire recurring job registration

---

## 🧩 Hangfire Configuration

* In-memory Hangfire storage (development only)
* Single worker queue initially
* Retry disabled temporarily for deterministic behavior

---

## 🧩 Logging

* ISyncLogger (real implementation)
* CorrelationId generated per sync run
* Structured logging only
* CorrelationId propagated through Hangfire job context

---

## 🔁 Execution Flow

Hangfire Recurring Job Trigger

→ Dispatcher creates SyncJob

→ Job enqueued into Hangfire

→ SyncModuleExecutor executes module

→ Result returned

→ Structured logs written with CorrelationId

---

## 🧪 Fake Module Behavior

FakeSyncModule:

* Accepts SyncContext
* Simulates processing delay
* Returns SyncResult (success/failure)
* Has no external dependencies

---

## 🚫 Explicitly NOT Included

* RabbitMQ
* External queues
* Database persistence
* Event system
* Hooks system
* Outbox / inbox patterns
* External API integration

---

## 📦 Output

* Fully runnable Hangfire-based Sync.Host service
* Deterministic execution loop
* Fake module execution working end-to-end
* Correlation-based logging visible across flow
* Hangfire dashboard available for inspection

---

## ✅ Success Criteria

* Worker runs continuously without external systems
* Dispatcher → Hangfire → Executor flow works reliably
* Modules are resolved dynamically via registry
* Logs contain full execution trace per sync run
* Failed jobs visible inside Hangfire dashboard
* System is observable without eventing infrastructure

---

## 📌 Key Design Rule

Phase 1 is the discovery phase:

> If something feels necessary (events, hooks, messaging), it is NOT added yet — it is recorded as a future requirement after observing real runtime behavior.

---

# 🟠 Phase 2 — Durable Hangfire Infrastructure

## Goal

Move from development runtime into durable production-safe background processing using persistent Hangfire storage.

---

## 🧩 Implemented Components

* SQL Server Hangfire storage
* Persistent Hangfire job queues
* Dedicated queues:

  * students-sync
  * finance-sync
  * hr-sync
  * push-sync
* Background job scheduling abstraction
* Retry policies
* Delayed jobs
* Recurring jobs

---

## 🧩 Cross-Cutting Additions

* CorrelationId propagation through Hangfire jobs
* ISyncLogger integrated into all job pipelines
* Job execution metadata
* Failure tracking

---

## 🔁 Execution Flow

Recurring Scheduler

→ Dispatcher

→ Hangfire Queue

→ Sync Executor

→ Module Execution

→ Retry / Success / Failure Handling

→ Structured Logging

---

## 🚫 Explicitly NOT Included

* RabbitMQ
* Distributed event bus
* Outbox / inbox patterns
* Hooks system
* Real-time streaming

---

## 📦 Output

* Durable distributed-ready job processing
* Persistent background jobs across restarts
* Reliable retry behavior

---

## ✅ Success Criteria

* Jobs survive application restart
* Failed jobs retry safely
* CorrelationId survives async execution
* Queue isolation works correctly

---

# 🔴 Phase 3 — Persistence Layer

## Goal

Add durability, recovery, traceability, and checkpoint tracking for sync execution.

---

## 🧩 Database Tables

* sync_runs
* sync_jobs
* sync_checkpoints
* sync_failures
* sync_dead_letters

---

## 🧩 Services

* ISyncCheckpointStore (EF Core implementation)
* ISyncJobStore
* SyncRunRepository
* FailureRepository

---

## 🧩 Cross-Cutting Additions

* CorrelationId persisted in all sync tables
* Full execution traceability
* Sync execution auditing
* Retry metadata persistence

---

## 📦 Output

* System survives restarts safely
* Full execution traceability enabled
* Durable checkpoints

---

## ✅ Success Criteria

* Checkpoints prevent reprocessing
* Execution history fully queryable
* No sync state loss after restart

---

# 🟣 Phase 4 — Sync Engine (Core Pipeline)

## Goal

Build the actual synchronization pipeline with full lifecycle integration.

---

## 🧩 Implemented Components

* SyncPipeline
* BatchProcessor
* ChangeDetector
* MergeEngine
* IdempotencyHandler
* MappingEngine

---

## 🧩 Cross-Cutting Integration

* ISyncLogger for structured pipeline logging
* Correlation propagation across batches
* Pipeline execution metrics

---

## 🔁 Execution Flow

Sync Job

→ Fetch Data

→ Chunk Batches

→ Validate + Map + Upsert

→ Persist Checkpoint

→ Complete Sync

---

## 📦 Output

* Fully working sync engine (without real modules yet)

---

## ✅ Success Criteria

* Idempotent execution guaranteed
* Batch execution works safely
* Checkpoint recovery works correctly
* Pipeline fully observable

---

# 🟤 Phase 5 — First Real Module (Students.Sync)

## Goal

Introduce the first real domain module using the full architecture stack.

---

## 🧩 Implemented Components

* StudentSyncModule
* StudentExtractor
* StudentMapper
* StudentValidator
* StudentWriter

---

## 🧩 Cross-Cutting Requirements

* Uses ISyncLogger for structured logs
* Correlation tracking preserved
* No direct infrastructure leakage outside boundaries

---

## 🔁 Execution Flow

External Students

→ Extract

→ Map

→ Validate

→ Upsert Internal DB

→ Persist Sync Result

---

## 📦 Output

* First real production sync flow

---

## ✅ Success Criteria

* End-to-end student sync works
* Full checkpoint recovery works
* Logs provide full observability
* Module remains infrastructure-isolated

---

# 🟢 Phase 6 — Push Sync (Internal → External)

## Goal

Enable outbound synchronization safely using Hangfire orchestration.

---

## 🧩 Implemented Components

* PushSyncModule
* External API client abstraction
* Outbound job scheduler
* Delayed retry jobs

---

## 🧩 Cross-Cutting Additions

* CorrelationId preserved across outbound calls
* Structured outbound logging
* Failure persistence

---

## 🔁 Execution Flow

Internal Change

→ Hangfire Job Scheduled

→ External API Sync

→ Retry / Failure Handling

→ Completion Tracking

---

## 📦 Output

* Fully bidirectional sync system

---

## ✅ Success Criteria

* Push sync fully asynchronous
* Retries handled safely
* External failures traceable
* No blocking request pipeline

---

# 🔵 Phase 7 — Multi-Module Expansion

## Goal

Extend system to multiple independent business domains.

---

## 🧩 Modules

* Students.Sync
* Staff.Sync
* Courses.Sync
* Schedule.Sync
* Finance.Sync

---

## 📦 Output

* Full modular sync ecosystem

---

## ✅ Success Criteria

* Modules isolated and independently deployable
* Queue separation maintained
* Failures isolated per module

---

# 🟡 Phase 8 — Performance Optimization

## Goal

Handle large-scale datasets efficiently.

---

## 🧩 Improvements

* EFCore.BulkExtensions evaluation
* Dapper evaluation for heavy reads
* Parallel batch execution
* Streaming large datasets
* Queue concurrency tuning
* Batch size tuning
* Worker scaling

---

## 🧩 Hangfire Optimizations

* Dedicated worker pools
* Queue prioritization
* Concurrency limits
* Rate limiting
* Long-running job optimization

---

## 📦 Output

* High-performance sync platform

---

## ✅ Success Criteria

* Large sync jobs complete within SLA
* Memory usage remains controlled
* No queue starvation
* Database pressure minimized

---

# 🔴 Phase 9 — Resilience Hardening

## Goal

Production-grade stability and recovery tooling.

---

## 🧩 Features

* Dead-letter handling
* Poison job isolation
* Manual replay tools
* Retry backoff policies
* Circuit breakers
* Partial sync recovery
* Job cancellation support

---

## 📦 Output

* Production-safe resilient sync infrastructure

---

## ✅ Success Criteria

* Failed jobs recover safely
* Replay operations deterministic
* Poison jobs isolated automatically

---

# ⚫ Phase 10 — Observability Layer

## Goal

System-wide monitoring and operational visibility.

---

## 🧩 Features

* Sync metrics tables
* Health checks
* Failure dashboards
* Queue monitoring
* Job duration metrics
* Lag monitoring
* Throughput metrics
* Alerting hooks

---

## 🧩 Monitoring Integrations

* Hangfire Dashboard
* OpenTelemetry (optional)
* Prometheus/Grafana (optional)

---

## 📦 Output

* Fully observable sync platform

---

## ✅ Success Criteria

* Operational bottlenecks visible
* Sync failures easily diagnosable
* Queue pressure measurable
* System health observable in real time

---

# 🧭 Execution Order (Strict)

Phase 0 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7 → Phase 8 → Phase 9 → Phase 10
---

# ⚡ Core Rules

Every phase must:

* run independently
* be testable
* not depend on future phases
* produce observable output
* include rollback-safe implementation
* preserve backward compatibility
* generate a detailed audit/output document after completion

---

# 📌 Architectural Constraint

Hangfire is the orchestration engine — not the business engine.

Business synchronization logic must remain independent from:

* Hangfire APIs
* Queue implementation details
* Infrastructure-specific concerns

The sync engine must always remain portable to another orchestration mechanism if future scaling requires it.
