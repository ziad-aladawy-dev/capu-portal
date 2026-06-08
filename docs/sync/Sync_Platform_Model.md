# Modular Bidirectional Sync Service Architecture

## Overview

This document defines the architecture for a modular synchronization service responsible for synchronizing data between:

* External University System (Source of Truth)
* Internal Portal System

The sync service must:

* Support modular architecture
* Support bidirectional synchronization
* Handle large-scale datasets efficiently
* Use queue-based asynchronous processing
* Support retries and resiliency
* Allow future modules to integrate easily
* Support scheduled and on-demand synchronization
* Maintain consistency while preserving scalability

---

# Core Architecture Principles

## 1. External System Is the Source of Truth

The external system owns authoritative data.

Implications:

* External updates override internal state
* Internal changes must propagate back externally
* Synchronization conflicts resolve in favor of external system
* Internal entities must maintain stable references to external entities

Example:

```text
External Student Record
        ↓
Internal Portal Student Record
```

---

## 2. Queue-Based Synchronization

Synchronization must be asynchronous.

Reasoning:

* Large student datasets
* High synchronization density
* Retry requirements
* Failure isolation
* Scalability requirements
* Long-running operations

The system must never perform direct synchronization inside API requests.

Correct flow:

```text
Request
   ↓
Store Internal Change
   ↓
Publish Queue Job
   ↓
Background Worker Processes Sync
```

---

## 3. Modular Ownership

Each business module owns its own synchronization implementation.

Example:

```text
Modules/
├── Students.Sync
├── Finance.Sync
├── HR.Sync
├── Courses.Sync
└── ...
```

The central sync host only:

* schedules jobs
* dispatches work
* coordinates queues
* handles retries
* tracks execution

Business rules remain inside modules.

---

# High-Level Architecture

```text
                ┌────────────────────┐
                │ External Database  │
                └─────────┬──────────┘
                          │
                Extract / Pull Changes
                          │
                          ▼
                ┌────────────────────┐
                │ Sync Dispatcher    │
                └─────────┬──────────┘
                          │
          ┌───────────────┼────────────────┐
          │               │                │
          ▼               ▼                ▼

 ┌────────────────┐ ┌────────────────┐ ┌────────────────┐
 │ Students Queue │ │ Finance Queue  │ │ HR Queue       │
 └────────┬───────┘ └────────┬───────┘ └────────┬───────┘
          │                  │                  │
          ▼                  ▼                  ▼

 ┌────────────────┐ ┌────────────────┐ ┌────────────────┐
 │ Students.Sync  │ │ Finance.Sync   │ │ HR.Sync        │
 └────────┬───────┘ └────────┬───────┘ └────────┬───────┘
          │                  │                  │
          ▼                  ▼                  ▼

             Internal Portal Database
```

---

# Synchronization Directions

The system supports two synchronization directions.

---

# Flow A — External → Internal

Primary synchronization flow.

## Example

Student updated in university system.

```text
External Database
    ↓
Detect Changed Rows
    ↓
Enqueue Hangfire Jobs
    ↓
Students.Sync Worker
    ↓
Map + Validate
    ↓
Upsert Internal Records
```
Note: Detect Changed Rows -> RowVersion handling (not including auth, cross-cutting)

This flow runs:

* daily scheduled syncs
* admin-triggered syncs
* repair/reconciliation syncs

---

# Flow B — Internal → External

When internal admins modify portal data.

```text
Portal Update
    ↓
Save Internal Change
    ↓
Store Outbox Event
    ↓
Enqueue Hangfire Job
    ↓
External Sync Worker
    ↓
External System Updated
```

This flow must always be asynchronous.

Never directly update external systems inside request lifecycle.
note: External source must be always stable and the highes priority (source of truth)

---

# Synchronization Model

The synchronization process behaves conceptually like a distributed merge/upsert pipeline.

For each incoming entity:

```text
Incoming External Record
        ↓
Find Internal Entity By ExternalId
        ↓
Exists?
 ├── No  → Insert
 └── Yes → Update
```

---

# Merge Identity

Every synchronized entity must maintain stable external identity.

Example:

```text
Students
---------
Id
ExternalStudentId
ExternalUpdatedAt
ExternalVersion
LastSyncedAt
```

The merge key is:

```text
ExternalStudentId
```

Never use:

* internal generated IDs
* row positions
* temporary identifiers

---

# Synchronization Metadata

Each synchronized entity should contain:
implementation must be based on interfaces and inhertince to elimnate the chance of inconsistencies

| Field             | Purpose                    |
| ----------------- | -------------------------- |
| ExternalId        | identity mapping           |
| ExternalUpdatedAt | detect changes             |
| ExternalVersion   | version tracking           |
| LastSyncedAt      | operational tracking       |
| OriginSystem      | loop prevention            |

---

# Conflict Resolution Strategy

Since external system is authoritative:

| Scenario         | Result                   |
| ---------------- | ------------------------ |
| External updated | External wins            |
| Internal updated | Push externally          |
| Both updated     | External wins            |
| External deleted | Internal soft-delete     |
| Internal deleted | Push deletion externally |

---

# Preventing Sync Loops

Potential issue:

```text
Portal Update
    ↓
Push To External
    ↓
External Change Detected
    ↓
Pulled Back Internally
    ↓
Infinite Loop
```

Mitigation:

* source markers
* sync version tracking
* change hashes
* origin metadata

Example:

```text
OriginSystem
LastSyncedVersion
LastSyncHash
```

---

# Incremental Synchronization

The system should prioritize incremental synchronization.

Avoid full table scans repeatedly.

Preferred strategies:

```text
UpdatedAt > LastCheckpoint
```

or:

```text
RowVersion
CDC
Change Tracking
```

---

# Synchronization Modes

## 1. Scheduled Daily Sync

Purpose:

* large student reconciliation
* consistency verification
* repair missed changes

Example:

```text
Hangfire Recurring Job
Daily at 2 AM
```

---

## 2. On-Demand Admin Sync

Admins may trigger:

* student sync
* module sync
* single entity sync
* hourly refreshes
* emergency reconciliation

These operations enqueue Hangfire background jobs only.

Never process synchronously inside request.

Refreshing pages Does Not affect the queue nor the sync request.

---

## 3. Repair/Reconciliation Sync

Purpose:

* recover failed batches
* repair inconsistencies
* replay failed jobs

---

# Queue Design

Recommended Hangfire queue structure:

```text
students.sync.pull
students.sync.push
finance.sync.pull
finance.sync.push
hr.sync.pull
hr.sync.push
```

Optional:

```text
failed jobs storage
scheduled retries
priority queues
dedicated worker queues
```

---

# Outbox Pattern

Internal updates should use outbox pattern.

Example transaction:

```text
1. Save Internal Entity
2. Save Outbox Event
3. Commit Transaction
4. Enqueue job
```

Background workers later process outbox events.

Benefits:

* resiliency
* consistency
* retry support
* no lost updates

---

# Recommended Processing Pipeline

## Pull Pipeline

```text
Read Checkpoint
    ↓
Fetch External Changes
    ↓
Chunk Into Batches
    ↓
Enqueue Hangfire Jobs
    ↓
Workers Process Batches
    ↓
Validate
    ↓
Map
    ↓
Upsert
    ↓
Store Checkpoint
```

---

## Push Pipeline

```text
Internal Update
    ↓
Store Outbox Event
    ↓
Enqueue Hangfire job
    ↓
Worker Sends External Update
    ↓
Mark Event Processed
```

---

# Batch Processing Strategy

Never process massive datasets in:

* single transactions
* single memory loads
* single requests

Use chunked processing.

Example:

```text
500 rows
1000 rows
2000 rows
```

depending on payload size.

---

# Retry Strategy

Retries must:

* be asynchronous
* support exponential backoff
* remain idempotent

Retries are managed by Hangfire retry policies and delayed jobs

Example:

```text
1 minute
5 minutes
15 minutes
1 hour
```

After retry exhaustion:

```text
Dead Letter Handling / Failed Jobs Storage
```

---

# Idempotency Requirements

Every synchronization operation must be safely repeatable.

Duplicate execution must not corrupt state. !! Most Important

Recommended protections:

* upserts
* external unique keys
* version checks
* processed message tracking

---

# Recommended Database Tables

## Operational Sync Tables

```text
sync_runs
sync_jobs
sync_failures
sync_checkpoints
sync_dead_letters
outbox_messages
inbox_messages
```

---

# Recommended Project Structure

```text
src/
├── 1.API/
│
├── 2.Core/
│   ├── CapitalUniversity.Core.Abstractions/
│   ├── CapitalUniversity.Core.Application/
│   ├── CapitalUniversity.Core.Domain/
│   └── CapitalUniversity.Core.Infrastructure/
│
├── 3.SharedKernel/
│
├── 4.Modules/
│
├── 5.Sync/
│   ├── CapitalUniversity.Sync.Host/
│   ├── CapitalUniversity.Sync.Abstractions/
│   ├── CapitalUniversity.Sync.Infrastructure/
│   │
│   ├── CapitalUniversity.Sync.Student/
│   ├── CapitalUniversity.Sync.Payments/
│   ├── CapitalUniversity.Sync.Schedule/
│   ├── CapitalUniversity.Sync.CourseOffering/
│   └── CapitalUniversity.Sync.StudentServices/
│
└── 6.Application/
```



---

# Recommended Module Structure

Example:

```text
Students.Sync/
├── Consumers/
├── Producers/
├── Jobs/
├── Extractors/
├── Mappers/
├── Validators/
├── Writers/
├── Contracts/
├── Checkpoints/
└── Configuration/
```

---

# Suggested Core Interfaces

## Module Contract

```csharp
public interface ISyncModule
{
    string ModuleName { get; }

    Task PullAsync(SyncContext context);

    Task PushAsync(SyncContext context);
}
```

---

## Checkpoint Store

```csharp
public interface ISyncCheckpointStore
{
    Task<SyncCheckpoint?> GetAsync(string module);

    Task SaveAsync(
        string module,
        SyncCheckpoint checkpoint);
}
```

---

# Recommended Technology Stack

| Concern         | Recommendation              |
| --------------- | --------------------------- |
| Runtime         | .NET Worker Service         |
| Queue/bg jobs   | Hangfire                    |
| Scheduling      | Hangfire Recurring Jobs     |
| Retries         | Hangfire Retry Policies     |
| Logging         | Existing in system          |
| Metrics         | Basic Logging + DB Tracking |
| Bulk Operations | EFCore.BulkExtensions       |
| Discovery       | Manual DI Registration      |
| Persistence     | Hangfire SQL Server Storage |

---
