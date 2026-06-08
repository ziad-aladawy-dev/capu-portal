# Registered Courses Module Specification

## Overview

Implement a Registered Courses module inside:

```text
src/4.Modules
```

This module is responsible for providing read-only access to course registrations synchronized from external academic systems.

The portal does not manage course registration.

All registration data originates from external systems and is synchronized through the synchronization platform.

This module is a read-heavy module.

The module must not implement registration workflows.

---

# Responsibilities

The module is responsible for:

1. Displaying current registered courses.
2. Displaying historical course registrations.
3. Providing semester-based course enrollment history.
4. Providing course-attempt history.
5. Serving as the authoritative Student ↔ Course relationship within the portal.

---

# Important Constraints

DO NOT:

* Register students in courses.
* Drop courses.
* Modify registrations.
* Manage waitlists.
* Implement synchronization jobs.

DO:

* Store synchronized registration information.
* Expose registration information through queries.
* Support historical course attempts.
* Support transcript and grades modules.
---

# Sync Rule (CRITICAL)

DO NOT implement any synchronization logic inside this module.

Instead:

Assume Sync Platform will call repositories/services here
This module only exposes:
upserts for registration snapshots (if needed by architecture)
or direct EF Core persistence layer usage depending on existing conventions

If the project already uses strict read-model insertion patterns, follow that.

---

# Domain Design

The module owns the relationship:

```text
Student
    |
StudentRegisteredCourse
    |
Course
```

Suggested entity:

```csharp
StudentRegisteredCourse
{
    Id

    StudentId

    CourseId

    StructureNodeId

    AttemptNumber

    RegistrationStatus

    ExternalReferenceId

    RegisteredAt

    CompletedAt

    SyncedAt
}
```

Adapt naming according to existing conventions.

---

# Semester Support

Semester information must be derived from:

```text
UniversityStructureNode
```

Do not duplicate semester definitions.

Use existing hierarchy for:

* Academic year
* Semester
* Ordering

---

# Course Attempt Rules

Students may repeat courses.

Example:

Calculus I
Fall 2022

Calculus I
Fall 2023

Each registration attempt should be stored independently.

Attempt ordering must be preserved.

---

# CQRS Requirements

## Queries

### GetRegisteredCoursesQuery

Returns currently active registrations.

---

### GetStudentRegistrationHistoryQuery

Returns all historical registrations grouped by semester.

---

### GetCourseAttemptsQuery

Returns all attempts for a specific course.

---

# DTOs

Implement DTOs for:

```text
RegisteredCourseDto
SemesterRegistrationDto
CourseAttemptDto
```

---

# API Endpoints

## Student Endpoints

### Current Courses

GET /api/courses/registered

---

### Registration History

GET /api/courses/history

---

### Course Attempts

GET /api/courses/{courseId}/attempts

---

# Security

Reuse existing authorization framework.

Students may only access their own registrations.

---

# Performance Requirements

Queries should:

* Avoid N+1 queries
* Use projection-based queries
* Support large registration histories

---

# Expected Outcome

The final module should provide:

* Current registrations
* Historical registrations
* Course attempt history
* Student ↔ Course relationship for the portal
* External-system-friendly registration storage
