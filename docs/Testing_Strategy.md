# CapitalUniversity — Controller Testing & Concurrency Strategy

## 1. Purpose

This document defines the **mandatory testing strategy for all API controllers** in the system.

It ensures:
- Tests validate real HTTP behavior (not isolated unit mocks)
- Concurrency and SQL correctness are verified under parallel execution
- Performance regressions are caught at API level (not only service level)
- Every new endpoint is production-realistic

---

## 2. Core Principle

> Every critical endpoint must be tested as if it is receiving real production traffic.

This means:

- No “single-request happy path only” tests
- No mock-only validation of business logic
- No isolated service tests replacing API validation

Instead:

✔ Real HTTP pipeline  
✔ Real database (Testcontainers SQL Server)  
✔ Real DI container  
✔ Real EF Core behavior  
✔ Parallel execution when applicable  

---

## 3. Test Layers (Required Structure)

Each controller MUST have tests in 3 categories:

### A. Functional API Tests (Single Request)
Validate correctness of endpoint behavior.

Examples:
- Create returns correct response
- GetById returns 404 when missing
- Update modifies data correctly

Scope:
- 1 request at a time
- No concurrency assumptions

---

### B. Concurrency Tests (MANDATORY for write operations)

These tests simulate **real race conditions using parallel HTTP calls**.

#### Rules:
- Use `Task.WhenAll`
- Use 5–50 parallel requests depending on scenario
- Must hit REAL SQL Server (Testcontainers)
- Must NOT mock DbContext or services

#### Must be validated:

| Scenario | Expected Outcome |
|----------|-----------------|
| Unique constraints | Only 1 success, rest fail with SQL 2601/2627 |
| Idempotency keys | Only 1 insertion persists |
| RowVersion updates | No lost updates |
| Capacity-limited operations | Exactly N succeed |
| Soft delete concurrency | Final state consistent |

---

### Example Pattern:

```csharp
var tasks = Enumerable.Range(0, 20)
    .Select(_ => client.PostAsync("/api/students", payload));

var responses = await Task.WhenAll(tasks);
```