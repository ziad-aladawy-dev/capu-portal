# Academic Records Module Specification

## Overview

Implement an Academic Records module inside:

```text
src/4.Modules
```

This module combines:

* Grades History
* Academic Summary
* Transcript Generation
* Transcript PDF Export

The module must reuse registration data from the Registered Courses module.

The module must not own Student ↔ Course enrollment relationships.

All grades, GPA values, CGPA values, academic standing information, and transcript information are synchronized from external academic systems.

This module is read-heavy.

---

# Responsibilities

1. Semester grade history.
2. Semester grade details.
3. Academic summary.
4. Transcript generation.
5. Transcript PDF generation.
6. Storage of synchronized academic results.

---

# Important Constraints

DO NOT:

* Calculate GPA.
* Calculate CGPA.
* Manage registrations.
* Modify grades.
* Enter grades.
* Implement synchronization jobs.

DO:

* Store synchronized academic outcomes.
* Generate transcript views.
* Generate transcript PDFs.
* Expose academic history.

---

# Domain Design

This module depends on:

```text
StudentRegisteredCourse
```

from the Registered Courses module.

The module owns:

```text
StudentAcademicResult
```

Suggested entity:

```csharp
StudentAcademicResult
{
    Id

    StudentRegisteredCourseId

    Grade

    NumericScore

    Status

    CreditsEarned

    IsLatestAttempt

    ExternalReferenceId

    SyncedAt
}
```

---

# Academic Summary

Store synchronized values such as:

```text
Earned Credits
Remaining Credits
Passed Hours
Failed Hours
GPA
CGPA
Academic Standing
```

Do not calculate these values.

Suggested entity:

```csharp
AcademicSummarySnapshot
{
    Id

    StudentId

    GPA

    CGPA

    EarnedCredits

    RemainingCredits

    PassedHours

    FailedHours

    AcademicStanding

    SyncedAt
}
```

---

# Semester Grade History

Students must be able to view academic history grouped by semester.

Display order:

```text
Latest → Oldest
```

Semester information must be derived from:

```text
UniversityStructureNode
```

through StudentRegisteredCourse.

---

# Semester Grade Details

For each semester return:

* Semester
* Academic year
* Courses
* Grade
* Numeric score
* Status
* Credits earned

---

# Transcript Generation

Generate transcript data using:

```text
StudentAcademicResult
+
Academic Plan
+
Registered Courses
```

The transcript must group courses into:

### General Requirements

* Compulsory
* Elective

### Faculty Requirements

* Compulsory
* Elective

### Main Specialization Requirements

* Compulsory
* Elective

Reuse existing Academic Plan categorization whenever available.

---

# Transcript Attempt Rules

Only the latest attempt should appear in transcript output.

Historical attempts remain visible in grade history.

---

# Transcript Course Status Rules

Include all latest-attempt courses regardless of status:

* Passed
* Failed
* Withdrawn
* In Progress
* Incomplete

For non-completed courses:

```text
Grade: -
```

---

# CQRS Requirements

## Queries

### GetStudentSemesterHistoryQuery

Returns semester grade history.

---

### GetSemesterGradesQuery

Returns detailed semester grades.

---

### GetAcademicSummaryQuery

Returns synchronized academic summary values.

---

### GetStudentTranscriptQuery

Returns transcript structure.

---

### GenerateStudentTranscriptPdfQuery

Returns transcript PDF.

---

# DTOs

Implement DTOs for:

```text
SemesterHistoryDto
SemesterCourseDto
AcademicSummaryDto

TranscriptDto
TranscriptCategoryDto
TranscriptCourseDto
TranscriptPdfDto
```

---

# API Endpoints

## Grades

GET /api/grades/history

GET /api/grades/history/{semesterId}

GET /api/grades/summary

---

## Transcript

GET /api/transcript

GET /api/transcript/pdf

---

# Security

Reuse existing authorization framework.

Students may only access their own academic records.

---

# Performance Requirements

Queries should:

* Avoid N+1 queries
* Use projection-based queries
* Support large academic histories
* Efficiently generate transcripts

---

# Expected Outcome

The final module should provide:

* Semester grade history
* Academic summary
* Transcript generation
* Transcript PDF export
* Academic-result storage
* Integration with Registered Courses
* Integration with Academic Plan
