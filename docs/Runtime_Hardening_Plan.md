# Scoped Runtime Hardening Plan

## Scope Boundary

This remediation pass is LIMITED to:

* Cross-cutting infrastructure
* Courses
* Semesters
* Fees / Payments
* Student Information

Explicitly OUT OF SCOPE:

* Auth module business logic owned by teammates
* Frontend
* Permission management refactors
* Non-owned domain modules
* Large architectural rewrites
* DTO redesigns
* Entity redesigns

---

# 1. VALIDATION PIPELINE CONSOLIDATION

## Problem

MVC automatic FluentValidation execution conflicts with manual validation inside Application Services.

Current behavior:

* MVC short-circuits invalid requests before services execute
* Services still manually validate
* Different validation response shapes exist
* GlobalExceptionHandler is bypassed for model validation failures

This creates:

* dead validation code
* inconsistent API responses
* localization inconsistencies

---

## Scoped Fix Strategy

Keep validation INSIDE application layer only for owned modules.

DO NOT rewrite teammate modules.

---

## Tasks

### 1.1 Disable MVC automatic invalid model short-circuit

### File

`src/1.API/CapitalUniversity.API/Program.cs`

### Change

Configure:

```csharp
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});
```

Goal:

* allow requests to always reach application services
* unify validation response handling through GlobalExceptionHandler

---

### 1.2 Keep FluentValidation registration ONLY

Keep:

```csharp
AddFluentValidationAutoValidation()
```

ONLY for validator discovery/DI.

Do not rely on MVC automatic response generation.

---

### 1.3 Standardize manual validation in owned modules

Apply ONLY to:

* Courses
* Semesters
* Fees
* Student Information

Ensure every command/service:

* validates explicitly
* throws unified ValidationException

Pattern:

```csharp
var validation = await _validator.ValidateAsync(request, cancellationToken);

if (!validation.IsValid)
{
    throw ValidationExceptionFactory.Create(validation);
}
```

---

### 1.4 Verify no endpoint bypasses validation

Audit:

* Courses endpoints
* Semester endpoints
* Fees endpoints
* Student Information endpoints

Ensure:

* every write operation validates
* validators execute exactly once

---

# 2. LOCALIZATION HARDENING

## Problem

Validation messages and exception details leak hardcoded English strings.

Current state:

* localized titles
* non-localized details/messages

Result:

* mixed-language API payloads

---

## Scoped Fix Strategy

Localize ONLY owned-module validation and exceptions.

Do not touch teammate exception models.

---

## Tasks

### 2.1 Remove hardcoded validator messages

### Affected Areas

* Courses validators
* Semesters validators
* Fees validators
* Student Information validators

Replace:

```csharp
.WithMessage("EffectiveTo must be greater than EffectiveFrom.")
```

With localized keys:

```csharp
.WithMessage(LocalizationKeys.Courses.EffectiveToAfterFrom)
```

OR localized resolver helper already used in project.

---

### 2.2 Audit all owned exceptions

Replace hardcoded exception messages in:

* Courses services
* Semester services
* Payment/Fee services
* Student Information services

Avoid:

```csharp
throw new NotFoundException("Semester not found");
```

Prefer:

```csharp
throw new NotFoundException(LocalizationKeys.Semesters.NotFound);
```

---

### 2.3 Ensure GlobalExceptionHandler localizes Detail

### File

`GlobalExceptionHandler.cs`

Adjust ONLY shared cross-cutting behavior.

Goal:

* if exception.Message is localization key → localize it
* avoid mixed-language responses

---

### 2.4 Verify validation payload localization

Test:

* `Accept-Language: ar`
* invalid DTO payloads
* domain exceptions
* not found responses

Ensure:

* title localized
* detail localized
* validation errors localized

---

# 3. EXECUTION CONTEXT SAFETY

## Problem

Background jobs lose execution context because CurrentUser depends on HttpContext.

Result:

* missing actor IDs
* anonymous audit logs
* missing correlation IDs

---

## Scoped Fix Strategy

DO NOT redesign auth system.

ONLY harden cross-cutting behavior to degrade safely.

---

## Tasks

### 3.1 Audit background services in owned scope

Check:

* OutboxDispatcher
* Notification pipelines
* Semester scheduled jobs
* Academic timeline jobs
* Fee/payment async processing

Identify:

* where ICurrentUser is accessed
* where RequestContext is assumed

---

### 3.2 Prevent fake authenticated context

Ensure background jobs:

* never fabricate authenticated users
* explicitly use system actor identifiers

Example:

```csharp
Actor = SystemActors.BackgroundProcessor
```

instead of:

```csharp
CurrentUser.UserId
```

when unavailable.

---

### 3.3 Preserve correlation IDs where possible

For owned async pipelines:

* propagate correlation IDs through event payloads
* include correlation ID in audit logs

Do NOT introduce AsyncLocal architecture rewrite.

---

### 3.4 Add defensive logging

Where context is required but missing:

Log warning:

* missing user context
* missing correlation ID
* background execution path

Avoid silent failures.

---

# 4. EXCEPTION PIPELINE HARDENING

## Problem

Client-originated exceptions are logged as full application failures.

Result:

* noisy telemetry
* alert fatigue
* harder production debugging

---

## Scoped Fix Strategy

Improve severity classification ONLY.

Do not redesign exception architecture.

---

## Tasks

### 4.1 Reduce log severity for expected exceptions

### File

`GlobalExceptionHandler.cs`

Expected exceptions:

* ValidationException
* NotFoundException
* ForbiddenException
* UnauthorizedException

Should NOT log as:

```csharp
LogError
```

Prefer:

* Warning
* Information

Reserve Error for:

* infrastructure failures
* unhandled exceptions
* DB failures
* runtime faults

---

### 4.2 Add DbUpdateException handling

Map:

* unique constraint violations
* FK violations

To:

* 400
* or 409

Instead of generic 500.

Only add safe normalization logic.

Do NOT add provider-specific fragile parsing unless isolated.

---

# 5. VERIFICATION CHECKLIST

## Validation

* [ ] MVC no longer short-circuits invalid requests
* [ ] Services validate exactly once
* [ ] GlobalExceptionHandler handles validation responses
* [ ] Validation responses are consistent

---

## Localization

* [ ] No hardcoded English in owned validators
* [ ] Exception details localized
* [ ] Arabic responses verified
* [ ] Validation arrays localized

---

## Execution Context

* [ ] Background jobs no longer assume HttpContext
* [ ] Audit logs never silently lose actor identity
* [ ] Correlation IDs propagated where available
* [ ] Missing context generates warnings

---

## Exception Handling

* [ ] Client errors no longer pollute ERROR telemetry
* [ ] DB constraint violations normalized
* [ ] Unhandled exceptions still log as Error
* [ ] Response structure remains consistent

---

# Success Criteria

This pass is considered successful when:

* validation executes once consistently
* localization is end-to-end for owned modules
* background jobs stop silently losing identity
* audit logs become reliable
* API error payloads become consistent
* production logs become actionable
* no teammate-owned module behavior is broken
