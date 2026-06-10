---
name: student-portal-permissions-gap
description: Students created via POST /api/students get zero permissions, so the entire /student/* portal is unreachable for them
metadata:
  type: project
---

Verified live (2026-06-10): a student created through `POST /api/students` logs in successfully but `/auth/me` returns **zero permissions** and no user-type field — there is no "Student" role in the roles list (only Academic Advisor, Department Head, Registrar, Faculty Admin, Super Admin, Viewer, Staff). Student portal routes all require `student.*.view` permissions, so RouteGuard bounces such students out of `/student/*` entirely. Backend must auto-assign student permissions/role on creation (or seed a Student role). Until then the student portal can only be exercised with seeded students whose passwords are unknown.

**How to apply:** Don't attempt to live-test `/student/*` pages with API-created students. Frontend guard was fixed (2026-06-10) so unauthorized students land on `/student/login?session=unauthorized` instead of inside the admin shell — see [[advisor-hub-backend-endpoints]] for the other pending backend work.
