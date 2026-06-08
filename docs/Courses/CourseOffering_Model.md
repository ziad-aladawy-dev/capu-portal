Course Offering Model Design
Goal

Represent a runtime academic offering of a course for a specific academic context and academic term.

This is NOT the static course definition.

The CourseOffering represents:

a course opened for registration,
during a specific semester/term,
for a specific academic context (StructureNode),
with runtime registration behavior.

Schedules/timetable data MUST NOT live directly in the course entity. Schedules belong to ScheduleSlot entities attached to CourseOffering.

Core Design Rules
CourseOffering owns
runtime availability
registration state
academic targeting
offering-level constraints
offering lifecycle
CourseOffering does NOT own
schedule conflict logic
transcript logic
fee logic
prerequisite engine
workflow orchestration
timetable details
Minimal Initial Model

The first implementation should remain intentionally minimal.

Do NOT enforce new naming conventions. Do NOT introduce new architectural styles. Do NOT generate example implementations that may conflict with the existing codebase patterns.

Instead:

inspect the existing project structure,
follow the current naming conventions,
follow existing base entity patterns,
follow existing EF configuration patterns,
follow existing validation patterns,
and integrate naturally with the current module structure.

The initial version should only establish:

the entity,
core relationships,
minimal lifecycle behavior,
and safe extension points.

Avoid speculative abstractions. Avoid introducing infrastructure that the project does not already use.

OfferingStatus
public enum OfferingStatus
{
    Draft = 0,
    Open = 1,
    Closed = 2,
    Cancelled = 3
}
ScheduleSlot Direction

ScheduleSlot must belong to CourseOffering.

NOT:

Course -> Schedule

Correct:

CourseOffering -> ScheduleSlot

Reason:

one course can have many offerings,
each offering can have different schedules,
each offering can have multiple sections,
future registration depends on offering schedules.
Main Behavioral Logic

The CourseOffering module exists in the module layer and should behave as an isolated business module as much as possible within the current architecture constraints.

The module represents runtime academic availability.

Main logic responsibility:

a course becomes available through an offering,
offerings belong to academic terms,
offerings target academic structure nodes,
students will later register into offerings,
schedules attach to offerings,
offerings act as the runtime bridge between curriculum and timetable.

The module should remain focused on offering state and runtime availability only.

The module should NOT evolve into:

a registration engine,
a scheduling engine,
a transcript engine,
or a workflow orchestrator.

Complex workflows should remain outside the entity itself.

Manifest Guidance

This project uses manifests primarily as lightweight module descriptors and permission registration points.

The CourseOffering module should follow the same existing convention.

The manifest may contain:

permission registrations,
module identification metadata,
lightweight module exposure configuration.

Do NOT introduce:

plugin frameworks,
dynamic runtime loading systems,
advanced dependency resolution,
or speculative modular infrastructure.

The manifest should remain minimal and consistent with the existing project structure.

Important Architecture Constraints
The module must remain isolated

CourseOffering may reference:

Courses
AcademicTerms
StructureNodes

It must NOT:

manipulate students directly,
manipulate fees,
manipulate transcripts,
manipulate payments,
orchestrate workflows.
Future Extension Points

Allowed future additions:

instructor assignment
room assignment
online/hybrid delivery
registration windows

Do NOT implement these now.