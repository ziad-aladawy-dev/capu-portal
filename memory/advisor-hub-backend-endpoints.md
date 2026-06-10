---
name: advisor-hub-backend-endpoints
description: Admin-scoped student academic endpoints the Advisor Hub UI expects but backend must still add
metadata:
  type: project
---

The Advisor Student Hub (`/admin/students/:id/academics`, built on branch `feature/academic-workspaces-overhaul`) reads a SPECIFIC student's record, but the existing `transcript`/`grades`/`registered` APIs bind studentId from the JWT (student-self only). The user committed (2026-06-10) to adding these admin-scoped routes:

- `GET /api/students/{id}/transcript`     -> TranscriptDto      (perm `academic-records.transcript.view`)
- `GET /api/students/{id}/grades/summary` -> AcademicSummaryDto (perm `academic-records.grades.view`)
- `GET /api/students/{id}/grades/history` -> SemesterHistoryDto[]
- `GET /api/students/{id}/registered`     -> RegisteredCourseDto[]

Frontend already wired against these exact shapes in `core/services/studentAcademicsService.js` + `core/query/useStudentAcademics.js`. Until deployed, calls 404 and the UI shows a "endpoint pending" card (panels degrade gracefully; `retry:false` so no spinner). The Degree Audit panel still works off `usePlanByStructure` but shows completion only once the transcript endpoint is live.
