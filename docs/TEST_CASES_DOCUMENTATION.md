# Semester Module Test Documentation 🧪

This document provides a comprehensive overview of the test cases implemented for the Semester module, covering both unit tests (logic validation) and contract/integration tests (API behavior and edge cases).

## 1. Unit Tests (`Core.UniTests`)

These tests focus on the business logic within the `AcademicYearService` and `SemesterService`.

### Academic Year Service
- **CreateAsync_ShouldSetIsCurrent_WhenDateMatches**: Ensures that a newly created academic year is automatically marked as `IsCurrent` if the current date falls within its range.
- **CreateAsync_ShouldDeactivateExisting_WhenNewIsCurrent**: Verifies that when a new "current" year is created, any existing current year is deactivated.
- **CreateAsync_ShouldThrowValidationException_OnOverlap**: Ensures that overlapping academic years are rejected at the service level.
- **UpdateAsync_ShouldUpdateCurrentStatus**: Verifies that updating dates correctly recalculates the `IsCurrent` status.

### Semester Service
- **CreateAsync_ShouldThrowIfYearNotFound**: Ensures a semester cannot be created if the referenced Academic Year ID does not exist.
- **CreateAsync_ShouldThrowIfDatesOutsideYear**: Validates that semester dates must be strictly within the parent Academic Year's range.
- **CreateAsync_ShouldThrowOnOverlap**: Prevents multiple semesters within the same year from overlapping.
- **ResolveCurrentSemesterAsync_ShouldUpdateStatus**: Verifies the logic that automatically identifies and sets the active semester based on the current date.

---

## 2. Contract & Integration Tests (`Contract.Tests`)

These tests verify the end-to-end HTTP request/response flow, including middleware, routing, and database persistence (using an In-Memory provider).

### Academic Year API (`/api/academic-years`)

| Scenario | HTTP Method | Expected Result | Description |
| :--- | :--- | :--- | :--- |
| **Create Valid** | `POST` | `201 Created` | Successfully creates a year with valid dates. |
| **Create Overlapping** | `POST` | `400 Bad Request` | Rejects a year that overlaps with an existing one. |
| **Create Invalid Dates** | `POST` | `400 Bad Request` | Rejects if `EndDate` is before `StartDate`. |
| **Get By ID** | `GET` | `200 OK` | Returns the correct academic year details. |
| **Get Non-Existent** | `GET` | `404 Not Found` | Returns 404 for a random GUID. |
| **Update Valid** | `PATCH` | `200 OK` | Partially updates a year's name or dates. |
| **Update Non-Existent** | `PATCH` | `404 Not Found` | Returns 404 when trying to update a missing year. |
| **Delete Valid** | `DELETE` | `200 OK` | Removes the year and ensures it's no longer retrievable. |

### Semester API (`/api/semesters`)

| Scenario | HTTP Method | Expected Result | Description |
| :--- | :--- | :--- | :--- |
| **Create Valid** | `POST` | `201 Created` | Successfully creates a semester within its year's range. |
| **Create Outside Range** | `POST` | `400 Bad Request` | Rejects if semester dates are outside the parent year's range. |
| **Create Non-Existent Year**| `POST` | `400 Bad Request` | Rejects if the `AcademicYearId` does not exist. |
| **Create Overlapping** | `POST` | `400 Bad Request` | Rejects if it overlaps another semester in the same year. |
| **Update Outside Range** | `PATCH` | `400 Bad Request` | Rejects if an update moves dates outside the academic year. |
| **Get Current** | `GET` | `200 OK / 204` | Returns the currently active semester. |

---

## 3. Infrastructure & Error Handling

- **Global Exception Handler**: Verified that `ValidationException` is caught and transformed into a RFC 7807 `ProblemDetails` response with a `400` status code and detailed error messages.
- **Concurrency**: The `UniversityStructureSeeder` is tested to handle concurrent requests during test parallelization using a `SemaphoreSlim`.
- **Authentication**: All protected endpoints are tested to return `401 Unauthorized` when a valid JWT is missing.

---

## 4. Test Execution

To run the full suite:
```bash
dotnet test tests/Core.UniTests
dotnet test tests/Contract.Tests
```
