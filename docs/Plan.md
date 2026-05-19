# University Platform — Implementation Plan & Architectural Constraints

# Goal

This document defines:

- module boundaries
- architectural rules
- persistence rules
- authorization integration
- localization integration
- scope filtering integration
- caching constraints
- performance expectations
- implementation phases

The implementation MUST strictly follow the existing platform architecture.

---

# Mandatory References Before Implementation

Before implementing any feature, Claude MUST read and follow:

```text
RequestPipeline_Context_Authorization_and_Localization.md
Authorization_Model.md
```

These documents define:

- request context resolution
- authorization flow
- localization flow
- scope filtering
- permission resolution
- tenant/scope behavior
- middleware expectations

Implementation MUST integrate with them exactly.

---

# Critical Architectural Rules

# Never Touch Logic Outside Module Scope

Claude MUST NOT:

- modify unrelated services
- modify unrelated business logic
- rewrite existing workflows
- bypass existing abstractions
- tightly couple modules together

Changes must remain isolated to the target module/feature.

If integration is needed:

- use contracts
- use interfaces
- use middleware
- use extension points

Never break module boundaries.

---

# Core Layer Responsibilities

Core contains only:

- stable academic concepts
- foundational abstractions
- shared domain models
- shared contracts
- structure hierarchy
- academic planning

Core MUST remain abstract and extensible.

Core MUST NOT contain:

- operational workflows
- registration workflows
- transcript workflows
- GPA logic
- payment workflows
- dynamic profile workflows

---

# Module Responsibilities

# Courses Module (Core Layer)

Courses module owns:

- course catalog
- academic plans
- curriculum structure

Courses module MUST NOT own:

- prerequisites
- blocking rules
- registration validation
- enrollment logic
- transcript logic
- GPA logic

Those belong to future Registration module.

---

# Payments Module

Payments module owns:

- invoices
- invoice items
- transactions
- payment state
- gateway integration

Payments module acts as centralized financial infrastructure.

Other modules may create fees through contracts only.

---

# Student Information Module

Student Information owns:

- dynamic student profile data
- sparse student information
- sensitive optional records

The module must remain flexible and extensible.

---

# Persistence Rules

# CoreDbContext Rules

CoreDbContext contains only:

```csharp
DbSet<T>
```

Do NOT place configurations inside CoreDbContext.

---

# EF Configurations

Each module owns its configurations.

Example:

```text
Module
 └── Infrastructure
      └── Persistence
           └── Configurations
```

Load using:

```csharp
ApplyConfigurationsFromAssembly(...)
```

---

# Cross Module Rules

Modules MUST NOT:

- use cross-module EF navigations
- reference foreign persistence implementations
- directly manipulate another module's entities

Communication must happen through:

- contracts
- IDs
- events
- application services

---

# Authorization Integration

Implementation MUST integrate with the authorization model.

Reference:

```text
Authorization_Model.md
```

---

# Permission Manifest Requirement

Every module MUST expose:

```csharp
IPermissionManifest
```

Purpose:

- static permission discovery
- DB synchronization
- centralized authorization visibility

---

# Permission Sync Rules

Application startup should:

1. scan assemblies
2. discover manifests
3. synchronize permissions
4. preserve assignments
5. avoid duplication

---

# Scope Filtering Rules

Scope filtering MUST follow:

```text
RequestPipeline_Context_Authorization_and_Localization.md
```

The request context extracted from headers MUST participate in:

- query filtering
- authorization
- cache key generation
- data visibility

Never bypass scope filtering.

---

# Localization Rules

Localization MUST integrate with the request pipeline.

All user-facing messages MUST:

- use localization abstractions
- avoid hardcoded strings
- support multilingual expansion

Localized cache entries MUST include culture-aware keys.

Example:

```text
course:object:{id}:culture:en
course:object:{id}:culture:ar
```

---

# Middleware Rules

If required, custom middleware may be added.

Middleware MUST:

- follow existing request pipeline conventions
- remain isolated
- avoid breaking authorization flow
- avoid bypassing localization flow

Potential valid middleware:

- request context enrichment
- scope propagation
- cache invalidation dispatching

---

# Performance Requirements

The implementation must support:

- 10k+ concurrent users
- low DB contention
- low Redis memory pressure
- horizontal scalability
- low serialization overhead

Avoid architecture decisions that scale poorly.

---

# Mandatory Caching Strategy

The system uses:

- shared object caching
- user/scope reference caching

Reference:

```text
caching-strategy.md
```

---

# Cache Rules

Do NOT:

- duplicate payloads per user
- cache EF entities directly
- cache unrestricted global datasets
- bypass scope filtering
- store large nested object graphs

Cache DTO/read models only.

---

# Seeder Requirements

All modules MUST provide realistic seed data.

Seeder data should include:

- structure nodes
- academic plans
- sample courses
- invoices
- transactions
- student profile records
- permissions
- localization examples

Seeder data should support:

- integration testing
- authorization testing
- scope filtering testing
- localization testing
- caching behavior testing

---

# Courses Module Design

# Entities

## Course

```csharp
Course
- Id
- Code
- Title
- CreditHours
- CourseCategory
- IsActive
```

---

## AcademicPlan

```csharp
AcademicPlan
- Id
- StructureNodeId
- Name
- EffectiveFrom
- EffectiveTo
- IsActive
```

---

## AcademicPlanCourse

```csharp
AcademicPlanCourse
- Id
- AcademicPlanId
- CourseId
- Level
- Semester
- IsMandatory
```

---

# Explicit Non-Goals

Do NOT implement:

- prerequisites
- registration validation
- enrollment workflows
- transcripts
- GPA logic
- blocking logic

---

# Payments Module Design

# Required Entities

## Invoice

```csharp
Invoice
- Id
- StudentId
- Status
- TotalAmount
- CreatedAt
```

---

## InvoiceItem

```csharp
InvoiceItem
- Id
- InvoiceId
- Amount
- FeeType
- SourceModule
- ReferenceId
- Description
```

---

## PaymentTransaction

```csharp
PaymentTransaction
- Id
- InvoiceId
- Provider
- ProviderTransactionId
- Status
- RawPayloadJson
- IdempotencyKey
- CreatedAt
```

---

# Required Contracts

```csharp
IFeeCreationService
IPaymentVerificationService
```

---

# Student Information Design

# Entity

## StudentProfileRecord

```csharp
StudentProfileRecord
- Id
- StudentId
- Category
- SchemaVersion
- DataJson
- VerifiedBy
- VerifiedAt
- IsSensitive
```

---

# Categories

```csharp
MilitaryInformation
VaccinationInformation
EmergencyContact
DisabilityInformation
HousingInformation
Custom
```

---

# Student Information Rules

Do NOT:

- create key-value tables
- create hardcoded tables per profile type
- tightly couple schemas to regulations

Use JSON document records.

---

# API Rules

APIs MUST:

- follow existing conventions
- integrate authorization
- integrate localization
- integrate scope filtering
- support caching strategy
- avoid leaking unrelated data

---

# Logging & Auditing

Sensitive operations MUST support:

- audit logging
- correlation IDs
- request tracing
- payment tracing
- security visibility

Especially for:

- payments
- sensitive student records
- authorization-sensitive operations

---

# Testing Requirements

Implementation MUST include:

- unit tests
- integration tests
- authorization tests
- scope filtering tests
- localization tests
- caching behavior tests

---

# Forbidden Shortcuts

Do NOT:

- bypass authorization
- bypass localization
- bypass scope filtering
- directly access unrelated services
- create god services
- tightly couple modules
- duplicate cache payloads
- place operational workflows in Core

---

# Phase 1 — Courses Foundations

Implement:

- Course
- AcademicPlan
- AcademicPlanCourse
- configurations
- migrations
- permissions
- APIs
- localization
- caching integration
- tests
- seeders

Explicitly exclude registration logic.

---

# Phase 2 — Payments Foundations

Implement:

- Invoice
- InvoiceItem
- PaymentTransaction
- fee contracts
- payment verification
- authorization integration
- localization
- caching integration
- tests
- seeders

---

# Phase 3 — Student Information Foundations

Implement:

- StudentProfileRecord
- category enum
- JSON profile storage
- sensitive data handling
- authorization integration
- localization
- caching integration
- tests
- seeders

---

# Phase 4 — Permission Manifest Integration

Implement:

- IPermissionManifest
- manifest scanning
- DB synchronization
- permission seeders

---

# Final Constraint

The implementation must preserve:

- modularity
- extensibility
- authorization integrity
- localization integrity
- scope isolation
- performance scalability
- clean architecture boundaries
- future registration extensibility