## Goal
- Restore working admin schedule drag-and-drop and clicking while merging stash's course-grouped sidebar.

## Progress
### Done
- Replaced logo files and updated all four layout containers (admin Sidebar, student SideDrawer, student BottomNav, LandingNavbar) to 2:3 ratio with `object-fit: contain` and transparent backgrounds.
- Rewrote `StudentSchedule.jsx` + `StudentSchedule.module.css`: instructor names, section codes, course-grouped list view, legend, responsive compact mode.
- Backend: added `InstructorName` field to student schedule endpoint via `IStaffService`.
- Frontend hook: added `instructorName` mapping in `useStudentSchedule.js`.
- Admin `ScheduleSlotsPage.jsx` already had full CRUD, instructors via `useStaffOptions`, room clash detection, `backgroundSlots`, `handleMoveSlot`, `handleCellClick`, `dropdownGroups`, `LoadingSpinner`/`ErrorMessage`, and `getLocalized` — none were broken.
- Rewrote `DraggableScheduleGrid.jsx` as exact original from `git HEAD` (working drag/drop/click) but with sidebar replaced: `SectionRow` (course-grouped with search, instructor names, progress badges, drag grip) instead of `UnscheduledOffering`. No stash features merged — no ghost blocks, no `guarded`, no `onCellClick`, no `onMoveSlot`, no `addMinutesClamped`, no `useTranslation` for grid strings.
- Build passes with zero errors.

### In Progress
- (none)

### Blocked
- (none)

## Key Decisions
- Stash's `DraggableScheduleGrid.jsx` had advanced features (course groups, ghost blocks, guard, onCellClick, onMoveSlot) but was never committed and broke drag/drop. Reverted to exact committed `HEAD` version, only transplanting the course-grouped sidebar.
- `onCellClick` on `DroppableCell` removed — clicking empty cells previously did nothing, adding it caused unpredictable drawer opens.
- `onMoveSlot` in `handleDragEnd` removed — the user never had this working before; adding it broke existing slot dropping.
- Ghost blocks removed entirely — they conflicted with absolutely-positioned real slot blocks in the CSS grid layout.
- `backgroundSlots` / `showOthers` toggle is silently ignored (no-op) to avoid regressions.
- `useTranslation` not added to grid — existing hardcoded English strings kept unchanged.

## Relevant Files
- `frontend/src/modules/academic/pages/Schedule/DraggableScheduleGrid.jsx` — restored original + course-grouped sidebar only
- `frontend/src/modules/academic/pages/Schedule/scheduleSlots.css` — already has all section-row and course-group styles (unchanged)
- `frontend/src/modules/academic/pages/Schedule/ScheduleSlotsPage.jsx` — parent page (unchanged, passes unused `backgroundSlots`/`onMoveSlot`/`onCellClick` that component ignores)
- `frontend/src/modules/studentPortal/pages/StudentSchedule.jsx` — rewritten, working read-only view
- `frontend/src/modules/studentPortal/hooks/useStudentSchedule.js` — updated with instructor names, working
