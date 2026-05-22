# Schedule Module Design

## Goal

The Schedule module represents passive timetable metadata attached to CourseOfferings.

The module exists to describe:
- when an offering happens,
- where it happens,
- and what type of session it is.

The Schedule module is intentionally NOT responsible for:
- registration decisions,
- conflict resolution,
- attendance,
- instructor workloads,
- room booking workflows,
- transcript logic,
- or orchestration.

The module must remain simple, descriptive, and independently modifiable.

---

# Core Concept

A CourseOffering represents runtime academic availability.

A ScheduleSlot represents one scheduled session for that offering.

Example:

CourseOffering:
- CS101
- Fall 2026
- Section A

ScheduleSlots:
- Sunday 10:00–12:00 Lecture
- Tuesday 12:00–14:00 Lab

One CourseOffering may have multiple ScheduleSlots.

---

# Important Architectural Direction

The Schedule module is:
- descriptive,
- passive,
- metadata-focused.

The module is NOT:
- a scheduling engine,
- a registration engine,
- a conflict engine,
- or a calendar orchestration system.

This distinction is critical.

Future modules MAY USE schedule data:
- registration validation,
- clash detection,
- attendance,
- analytics,
- room utilization,
- notifications.

But Schedule itself must NOT own those behaviors.

---

# Module Positioning

This module belongs to the business/module layer.

It should behave as an isolated module even if physical boundaries are imperfect due to the current architecture.

The module should:
- own timetable metadata,
- expose minimal contracts,
- avoid workflow orchestration,
- avoid cross-module knowledge,
- remain independently modifiable.

---

# Allowed Dependencies

The Schedule module may reference:
- CourseOffering
- shared abstractions/common infrastructure already used by existing modules

The Schedule module should NOT directly depend on:
- Students
- Registration
- Payments
- Fees
- Transcript
- Attendance
- Notifications

---

# Main Entity

## ScheduleSlot

Represents a single scheduled occurrence/session attached to a CourseOffering.

Examples:
- Lecture
- Lab
- Tutorial
- Practical
- Exam Session

---

# Minimal Version

The minimal implementation should remain intentionally small.

Recommended minimal concepts:

- CourseOfferingId
- DayOfWeek
- StartTime
- EndTime
- SessionType
- Location (optional)
- DeliveryMode (optional)
- Notes (optional)

Minimal version goals:
- persistence,
- querying,
- future-safe structure,
- invariant validation.

No advanced workflows.

---

# Full Version (Future-Safe Direction)

The full version MAY later support:

## Scheduling Metadata
- recurrence patterns
- effective date ranges
- academic weeks
- timezone awareness
- cancellation markers
- replacement sessions

## Room Metadata
- room identifiers
- building identifiers
- virtual meeting metadata

## Instructor Metadata
- instructor assignment references
- assistant references

## Delivery Metadata
- online
- onsite
- hybrid

## Synchronization Metadata
- external system identifiers
- sync timestamps
- source systems

DO NOT implement these unless explicitly requested.

---

# Important Invariants

The module should validate:
- EndTime > StartTime
- valid DayOfWeek
- ScheduleSlot belongs to existing CourseOffering
- optional duplicate prevention if requested later

The module should NOT validate:
- student schedule conflicts
- instructor conflicts
- room conflicts
- semester conflicts
- registration eligibility

Those belong elsewhere.

---

# Critical Design Rules

## Rule 1 — Schedule is descriptive only

ScheduleSlot stores timetable data.

It does NOT decide:
- who can register,
- whether a conflict exists,
- or whether a room is available.

---

## Rule 2 — No workflow orchestration

Do NOT create:
- ScheduleOrchestrator
- TimetableEngine
- ConflictResolver
- CalendarCoordinator

unless future requirements explicitly justify them.

---

## Rule 3 — Avoid speculative abstractions

Do NOT introduce:
- generic recurrence engines,
- dynamic scheduling DSLs,
- plugin systems,
- generalized timetable frameworks.

Keep implementation proportional to current project maturity.

---

## Rule 4 — Keep Schedule attached to CourseOffering

Correct direction:

CourseOffering
    -> ScheduleSlots

Avoid:
- standalone schedule ownership,
- many unrelated parents,
- polymorphic scheduling systems.

---

# Testing Philosophy

Tests must validate:
- invariants,
- persistence correctness,
- modification safety,
- and meaningful business behavior.

Avoid:
- trivial getter/setter tests,
- fake coverage,
- mock-heavy meaningless tests.

Good tests:
- invalid time ranges rejected
- overlapping-slot policy (if implemented)
- proper offering attachment
- update behavior safety
- query filtering correctness

---

# Future Registration Relationship

Future Registration modules may:
- inspect ScheduleSlots,
- compare times,
- detect student clashes.

But registration logic must remain OUTSIDE the Schedule module.

Schedule provides data only.

---

# Manifest Guidance

If the project uses lightweight manifests:
- follow existing module conventions,
- register permissions minimally,
- avoid advanced modular infrastructure.

Do NOT introduce:
- runtime plugin systems,
- dependency graphs,
- dynamic loading frameworks.

---

# Performance Direction

Expected usage pattern:
- read-heavy,
- admin-managed,
- moderate write frequency.

Avoid premature optimization.

Do NOT add:
- caching layers,
- distributed scheduling systems,
- event streaming,
- real-time timetable synchronization

unless actual production pressure appears.

---

# Final Design Philosophy

The Schedule module should remain:
- simple,
- descriptive,
- stable,
- replaceable,
- and low-coupling.

It exists to provide timetable data safely.
