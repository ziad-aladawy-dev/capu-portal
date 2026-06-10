# 🏛️ CAPITAL UNIVERSITY — COMPREHENSIVE STUDENT PORTAL DEVELOPMENT SPECIFICATION

## Master Blueprint v2.0 (Customized for capu-portal)

---

## 🔷 SYSTEM OVERVIEW

**Project:** CapitalUniversity Portal (`capu-portal`)
**Architecture:** Dual-portal system (Admin/Staff + Student) with module-federated micro-frontend shell, modular monolith backend (Clean Architecture + CQRS)

### Existing Architecture Reference Points (Must Preserve & Extend)

The following architectural decisions are **already established** in the codebase and must be preserved, extended, and leveraged — not reinvented:

| Aspect | Current Implementation | Direction |
|--------|----------------------|-----------|
| **CSS** | Vanilla CSS with CSS custom properties (`:root`) — **NOT Tailwind** | Extend the design token system, add CSS modules pattern for encapsulation |
| **Language** | JavaScript (JSX) — **NOT TypeScript** | Phase-gated migration path to TypeScript; begin with strict JSDoc annotations on shared types, then incremental `.ts/.tsx` conversion |
| **React** | React 19 with hooks, `createElement` (no JSX in some runtime files) | Full JSX usage; leverage React 19 features (useOptimistic, use, Server Components via RSC-compatible routes) |
| **Routing** | `react-router-dom` v7 with manifest-driven route aggregation (`routeRegistry.js`) | Extend manifest with lazy-load metadata, breadcrumb inheritance, route-based code splitting |
| **Server State** | TanStack Query v5 (`@tanstack/react-query`), 30s staleTime, 5min gcTime | Add optimistic updates, infinite queries, background refetch intervals for critical data |
| **Client State** | Zustand v5 (2 stores: `useScopeStore`, `useAcademicStore`) + 5 React Contexts | Consolidate: keep scope/academic in Zustand, migrate away from AuthContext to Zustand middleware for auth persistence |
| **HTTP Client** | Axios with JWT interceptor, refresh queue, scope header injection | Add retry strategy, request deduplication, offline queue |
| **i18n** | i18next + react-i18next, Arabic (fallback `'ar'`) + English, 12 JSON files per language, RTL direction switching | Add locale-specific number/date formatting, pluralization rules, RTL-aware CSS logical properties audit |
| **UI Primitives** | Radix UI primitives (Dialog, DropdownMenu, Select, ToggleGroup, Tooltip) + Lucide icons | Standardize on Radix + Lucide; add missing primitives (Tabs, Accordion, Popover, Toast via Radix Toast) |
| **DnD** | `@dnd-kit` (core, sortable, utilities) | Leverage for course enrollment cart, dashboard widget reordering |
| **Forms** | `react-hook-form` + `zod` + `@hookform/resolvers` | Standardize all forms with this stack; add auto-save, dirty detection, wizard patterns |
| **Design Tokens** | CSS custom properties in `index.css` (navy `#1a1f5e`, gold `#c9a84c`, Space Mono heading, DM Sans body, Outfit UI) | Centralize into a design token file, add dark mode variants, semantic color aliases |
| **Permission Model** | `module.resource.action` with scope-aware resolution, route-level `RouteGuard`, UI-level `PermissionGate`, action levels 0-5 | Extend to support data-level row/field permissions |
| **Scope System** | Structural (university node) + Temporal (academic year/semester) scopes auto-attached via headers + query params | Add multi-scope selection, scope-based data filtering in all queries |
| **Architecture** | Manifest-driven modules with `routes.js` exports, lazy-loaded via `React.lazy` + `Suspense` | Add module-level ErrorBoundary, preloading strategies, route-based chunk splitting |
| **Module Federation** | `@originjs/vite-plugin-federation` configured as shell (no remotes yet) | Enable remote modules for plugin-based feature deployment |
| **Testing** | Vitest + jsdom + @testing-library/react (basic setup only) | Add comprehensive unit, integration, and E2E coverage |
| **Build** | Vite 8, `esnext` target, babel + react-compiler preset | Add bundle analysis, code splitting audit, PWA support |

---

## 🔷 PHASE 0: FOUNDATION AUDIT & TECHNICAL DEBT ELIMINATION

### 0.1 Dead Code & Cleanup

- **i18n file (`core/i18n/i18n.js`):** Remove the 873-line commented-out legacy translations block (lines 1–873) — it's a dead-code artifact from the earlier inline-resource approach. The actual implementation uses the 12-file JSON structure (lines 876+).
- **`App.css`:** Remove Vite template hero/counter styles — these are unused and conflicting.
- **`modules/auth/`:** Either populate with actual auth pages or remove — auth currently lives in `core/auth/`.
- **`modules.config.js`:** Either implement module registration or remove the empty file.
- **`src/assets/react.svg`, `src/assets/vite.svg`:** Remove Vite template artifacts.

### 0.2 Dependency Audit

- Audit all Radix UI packages against latest; consolidate version mismatches.
- Verify `@dnd-kit/sortable` v10 compatibility with `@dnd-kit/core` v6 — major version mismatch (v6 vs v10).
- Update `@hookform/resolvers` to align with `react-hook-form` v7.
- Check React 19 compatibility for all third-party hooks.

### 0.3 CSS Architecture Overhaul

- Centralize all CSS custom properties into `core/styles/tokens.css`.
- Add semantic token aliases:

```css
--color-primary: var(--navy-primary);
--color-primary-hover: var(--navy-accent);
--color-accent: var(--gold);
--color-accent-hover: var(--gold-light);
--color-surface: #ffffff;
--color-background: #f4f5f7;
--color-text-primary: #1a1f5e;
--color-text-secondary: #6b7280;
--color-border: #e5e7eb;
--color-success: #16a34a;
--color-warning: #d97706;
--color-error: #dc2626;
--color-info: #2563eb;
--font-heading: "Space Mono", monospace;
--font-body: "DM Sans", sans-serif;
--font-ui: "Outfit", sans-serif;
--radius-sm: 4px;
--radius-md: 8px;
--radius-lg: 12px;
--radius-xl: 16px;
--shadow-sm: 0 1px 2px rgba(0,0,0,0.06);
--shadow-md: 0 4px 12px rgba(26,31,94,0.12);
--shadow-lg: 0 8px 24px rgba(26,31,94,0.18);
--transition-fast: 150ms ease;
--transition-normal: 250ms ease;
--transition-slow: 350ms cubic-bezier(0.4,0,0.2,1);
```

- Add RTL-aware logical property usage audit: ensure all `left`/`right` → `inset-inline-start`/`inset-inline-end`, `margin-left` → `margin-inline-start`, etc.
- Add dark mode via `prefers-color-scheme: dark` and manual toggle with `.dark` class on `<html>`.
- Implement a CSS reset / normalization layer.
- Add print stylesheet for document export feature.

### 0.4 Environment Configuration

- Add `.env.development`, `.env.production`, `.env.example` files.
- Key variables:

```
VITE_API_BASE_URL=http://localhost:5256/api
VITE_WS_URL=ws://localhost:5256/ws
VITE_PAYMENT_GATEWAY_URL=https://payments.capitaluniversity.edu.eg
VITE_SENTRY_DSN=
VITE_APP_VERSION=$npm_package_version
VITE_DEPLOY_ENV=development
VITE_ENABLE_MOCKS=true
VITE_DEFAULT_LNG=ar
```

### 0.5 README & Documentation Overhaul

- Update `README.md` (currently references Vue 3 — completely outdated).
- Add:
  - Architecture overview
  - Getting started guide
  - Environment setup
  - Testing strategy
  - Deployment process
  - Contributing guidelines
  - Translation workflow

---

## 🔷 PHASE 1: AUTHENTICATION MODULE — ENHANCED

### 1.1 Current State Assessment

- Dual login portals (`/admin/login`, `/student/login`) — functional
- JWT with refresh token rotation — functional
- `AuthContext` with `useReducer` — functional but should migrate to Zustand
- `PermissionContext` with scope-aware permission checking — functional
- `RouteGuard`, `ProtectedRoute`, `PermissionGate` — functional
- `SessionTimeoutWarning` component — exists but only visual
- `ChangePasswordModal` — exists as component but NOT wired into auth flow
- `ForgotPasswordModal` — exists but NOT wired into auth flow
- **MISSING:** First-time login enforcement, password expiry check, auto-logout timer, idle detection

### 1.2 Authentication State Overhaul

**Migrate AuthContext to Zustand:**

```js
// core/stores/useAuthStore.js
const useAuthStore = create((set, get) => ({
  user: null,
  permissions: [],
  authorizedScopes: null,
  activeScope: null,
  isAuthenticated: false,
  isLoading: true,

  login: async (identifier, password) => { ... },
  logout: async () => { ... },
  refreshSession: async () => { ... },
  setActiveScope: (scope) => { ... },
}));
```

### 1.3 Login Flow Extensions

**National ID Validation:**
- Regex: `/^[0-9]{14}$/` — Egyptian National ID format (14 digits, numeric only)
- Frontend validation via zod schema + `react-hook-form`
- Input masking for readability (e.g., `###-####-####-###`)

**Password Validation:**
- Minimum 8 characters
- At least 1 uppercase, 1 lowercase, 1 digit, 1 special character
- Backend-validated password history (last N passwords)

### 1.4 First-Time Login & Password Lifecycle

**Flow:**

```
Login Success → Check Response
  ├── isFirstLogin: true → Force ChangePasswordModal (non-dismissable, no dashboard access)
  ├── passwordExpiryDate < now → Force ChangePasswordModal + warning message
  └── isFirstLogin: false, password valid → Dashboard access
```

**Implementation:**
- `POST /auth/login` response includes:

```json
{
  "token": "...",
  "refreshToken": "...",
  "user": { ... },
  "isFirstLogin": true,
  "passwordExpiryDate": "2026-08-15T00:00:00Z",
  "requiresPasswordChange": true
}
```

- Frontend `AuthGate` wrapper component checks these flags before rendering `<Outlet />`
- Password change form with: current password, new password, confirm, strength meter
- API: `POST /auth/change-password` with `{ currentPassword, newPassword, confirmPassword }`
- On password change success: dismiss modal, set `isFirstLogin = false`, proceed to dashboard

### 1.5 Session Management

**Token Lifecycle:**
- Access Token: 15 minutes
- Refresh Token: 7 days (sliding expiration)
- On token refresh: check `passwordExpiryDate` from refresh response

**Auto-Logout Warning:**
- `SessionTimeoutWarning` component polls every 30 seconds
- At 5 minutes to expiry: show dismissable banner "Your session will expire in 5 minutes"
- At 1 minute: show non-dismissable modal "Session expiring — click to extend"
- On expiry: auto-logout + redirect to login with `?session=expired` param
- Extend session: call `POST /auth/refresh` to bump expiry

**Idle Detection:**
- Monitor `mousemove`, `keydown`, `click`, `scroll`, `touchstart` events
- After 30 minutes of inactivity: show idle timeout warning
- After 35 minutes: auto-logout
- Configurable via settings (admin configurable range: 15–120 minutes)

**Concurrent Session Management:**
- Track session version in JWT claims
- On password change: increment version → invalidate all other sessions
- On forced logout (admin action): next request returns 401 with `reason: "session_revoked"`

### 1.6 Biometric Authentication (Future Enhancement)

- WebAuthn / Passkey support for passwordless login
- Fingerprint / Face ID on mobile devices
- Stored as second factor (password + biometric) or primary (biometric-only)

---

## 🔷 PHASE 2: MANDATORY DATA & INTERSTITIAL MIDDLEWARE (THE "BLOCKER" SYSTEM)

### 2.1 Design Principle

Certain states MUST be resolved before the student can access any standard page. These create a "blocker" middleware layer that intercepts navigation and renders mandatory forms.

### 2.2 Architecture

```jsx
// core/components/StudentBlockerGate.jsx
function StudentBlockerGate({ children }) {
  const { data: blockerState } = useBlockerState();

  if (blockerState.isLoading) return <FullPageSkeleton />;
  if (blockerState.requiresPasswordChange) return <ChangePasswordModal />;
  if (blockerState.profileCompleteness < 100) return <CompleteProfileWizard />;
  if (blockerState.hasPendingMandatoryAction) return <MandatoryActionCenter />;

  return children;
}
```

Place inside `DashboardLayout` → wraps all student routes inside `<StudentBlockerGate>`.

### 2.3 Profile Completeness Check

**Endpoint:** `GET /student/profile/completeness`
**Response:**

```json
{
  "overallPercentage": 65,
  "missingFields": [
    { "field": "emergencyContactName", "label": "Emergency Contact Name", "severity": "high" },
    { "field": "emergencyContactPhone", "label": "Emergency Contact Phone", "severity": "high" },
    { "field": "address", "label": "Permanent Address", "severity": "medium" },
    { "field": "guardianName", "label": "Guardian Name", "severity": "medium" }
  ]
}
```

**UI:**
- Stepped wizard with progress bar
- Sections: Personal Info, Contact Details, Emergency Contact, Guardian/Sponsor, Academic Background
- Each step is auto-saved on completion
- Cannot navigate away until all `severity: "high"` fields are filled
- After submission: `POST /student/profile/complete` → returns updated completeness

### 2.4 Mandatory Surveys & Actions

**Endpoint:** `GET /student/mandatory-actions`
**Response:**

```json
{
  "hasPending": true,
  "actions": [
    {
      "id": "semester-feedback-2026-spring",
      "type": "survey",
      "title": "Spring 2026 Semester Feedback",
      "description": "Please evaluate your courses this semester",
      "deadline": "2026-06-30T23:59:59Z",
      "priority": "high",
      "formSchemaUrl": "/api/forms/semester-feedback"
    },
    {
      "id": "conduct-2026",
      "type": "acknowledgment",
      "title": "Code of Conduct Acknowledgment 2026",
      "description": "Read and acknowledge the updated student code of conduct",
      "deadline": "2026-03-15T23:59:59Z",
      "priority": "medium",
      "documentUrl": "/api/documents/code-of-conduct-2026"
    }
  ]
}
```

**Dynamic Form Renderer:**
- Fetch form schema from `formSchemaUrl` (JSON Schema or custom format)
- Render using dynamic form builder (react-hook-form + zod from schema)
- Support field types: text, textarea, select, radio, checkbox, date, file upload, rating, scale (1-5)
- Support validation rules, conditional fields, sections
- On submit: `POST /student/mandatory-actions/{id}/submit`

**Acknowledgment Flow:**
- Render document (PDF viewer or HTML renderer)
- Scroll-to-bottom enforcement before enabling acknowledgment checkbox
- Digital signature field (type full name as signature)
- Submit: `POST /student/mandatory-actions/{id}/acknowledge`

### 2.5 Action Center UI

- Card-based layout showing each pending action
- Color-coded priority (red = high/overdue, yellow = medium, blue = low)
- Countdown timer for deadlines
- Cannot access any other route until all actions resolved
- Sidebar shows "X mandatory actions pending" banner at all times

---

## 🔷 PHASE 3: DASHBOARD & COMMAND CENTER (STUDENT PORTAL OVERHAUL)

### 3.1 Current State Assessment

- `StudentDashboard.jsx` — exists, functional, but:
  - Uses inline `useEffect` + `useState` instead of TanStack Query for ALL data fetching
  - No skeleton loaders (uses custom `<LoadingSpinner />`)
  - Hardcoded error handling without ErrorBoundary
  - No React.memo / useMemo optimization
  - Missing real-time data integration
  - No dark mode support
  - Not fully i18n'd (some strings hardcoded in English)

### 3.2 Data Fetching Overhaul

**Replace all `useEffect` + `useState` patterns with TanStack Query hooks:**

```js
// Before (current):
const [offeringCount, setOfferingCount] = useState(null);
useEffect(() => { fetchData(); }, [activeScope]);

// After (target):
const { data: academicOverview, isLoading } = useAcademicOverview(activeScope);
const { data: services } = useAvailableServices(user?.id);
const { data: recentTransactions } = useRecentTransactions(user?.id, 5);
const { data: pendingActions } = usePendingMandatoryActions();
const { data: upcomingSchedule } = useUpcomingSchedule(activeScope, { days: 7 });
const { data: unreadNotifications } = useUnreadNotificationCount();
```

### 3.3 Dashboard Widget Architecture

**Widget System:**
- Each widget is a standalone component with:
  - TanStack Query data fetching (prop-driven query key)
  - Skeleton loading state
  - Error state with retry
  - Empty state with icon + message
  - `React.memo` wrapping for render optimization
  - i18n support via `useTranslation()`
  - Dark mode via CSS custom properties

**Dashboard Layout:**

```
┌─────────────────────────────────────────────────────────┐
│  Welcome, [Student Name] !                    [Notif Bell] │
│  Academic Year 2025-2026 | Spring Semester              │
├──────────────────────┬──────────────────────────────────┤
│  PROFILE SNIPPET     │  ACADEMIC STATS                  │
│  ┌──────┐           │  ┌──────┐ ┌──────┐ ┌──────┐    │
│  │ Avatar│           │  │ GPA  │ │Credits│ │Courses│    │
│  └──────┘           │  │ 3.45 │ │  45  │ │   6  │    │
│  Ahmed Ali          │  └──────┘ └──────┘ └──────┘    │
│  2024001234         │                                   │
│  Academic Standing: │                                   │
│  🟢 Good            │                                   │
├──────────────────────┴──────────────────────────────────┤
│  TODAY'S SCHEDULE                   │ UPCOMING EXAMS    │
│  ┌──────────────────┐              │ ┌────────────────┐ │
│  │ CS301 - 9:00 AM  │              │ │ MATH301 - Jun15│ │
│  │ Room 301         │              │ │ PHYS301 - Jun18│ │
│  │ CS302 - 11:00 AM │              │ └────────────────┘ │
│  └──────────────────┘              │                    │
├─────────────────────────────────────────────────────────┤
│  ACTION CENTER                                         │
│  🔴 Unpaid Fees: EGP 12,500       🟡 Pending Request: 2│
│  🔵 Registration Opens: Jun 20    ⚪ New Notifications:5│
├─────────────────────────────────────────────────────────┤
│  QUICK LINKS                                           │
│  [📚 My Courses] [📝 Register] [📊 Grades] [📅 Schedule]│
│  [💰 Payments] [📋 Requests] [👤 Profile] [🔔 Notifs]  │
├─────────────────────────────────────────────────────────┤
│  AVAILABLE SERVICES                                     │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐        │
│  │ID Req│ │Hous- │ │Trans-│ │Health│ │Career│        │
│  │      │ │ing   │ │cript │ │      │ │Center│        │
│  └──────┘ └──────┘ └──────┘ └──────┘ └──────┘        │
└─────────────────────────────────────────────────────────┘
```

### 3.4 Specific Widget Specifications

**Profile Snippet Widget:**
- Avatar (first letter or uploaded image) + name + student ID + National ID
- Current GPA (with color: green ≥ 3.0, yellow ≥ 2.0, red < 2.0)
- Academic Standing (Good, Probation, Suspended) with icon
- Enrolled credits this semester
- Advisor name & contact
- Click avatar → navigate to `/student/profile`

**Today's Schedule Widget:**
- Current day's classes with time, room, instructor
- Time-ordered with "now" indicator
- Upcoming exam countdown
- Click → navigate to `/student/schedule`

**Financial Snapshot Widget:**
- Outstanding balance with EGP amount
- Next due date
- Payment progress bar (paid vs total)
- Late fee alerts
- Click → navigate to `/student/payments`

**Action Center Widget:**
- List of actionable items: unpaid fees, pending requests, registration windows, unread notifications
- Color-coded severity
- Dismissible items (optimistic UI update)
- Quick action buttons inline

**Quick Links:**
- 8-card grid with icons
- Animated on mount (staggered entrance)
- Recently visited highlighted

**Services Catalog Widget:**
- First 4-6 available services as cards
- "View All" link
- Search within services

**Notification Tray:**
- Bell icon with unread count badge
- Click → dropdown of last 5 notifications
- "See All" → navigate to `/student/notifications`
- Inline "Mark as read" button
- Real-time via WebSocket/SSE

### 3.5 Widget Drag-and-Drop Customization

- Enable widget reordering via `@dnd-kit`
- Persist layout preference to Zustand + localStorage
- Widget visibility toggle (show/hide specific widgets)
- Layout grid: 2-column on desktop, 1-column on mobile
- Save layout preference:

```json
{
  "widgetOrder": ["profile", "schedule", "stats", "actions", "quicklinks", "services"],
  "hiddenWidgets": ["services"],
  "layout": "2-column"
}
```

### 3.6 Animated Mounting

- Widgets fade-in + slide-up on mount with staggered delay (50ms per widget)
- Use CSS `@keyframes` or `requestAnimationFrame` (avoid Framer Motion to keep bundle small)
- Skeleton shimmer animation for loading states
- Number counter animation for stat values (0 → target value over 1 second)

---

## 🔷 PHASE 4: ACADEMIC MANAGEMENT & COURSE ENROLLMENT ENGINE

### 4.1 Current State Assessment

- `CourseRegistration.jsx` — exists but uses **hardcoded mock data**
- `StudentCourses.jsx` — fetches from API but displays "offerings" not "registered courses"
- `StudentSchedule.jsx` — fetches real data, good weekly timetable grid
- `StudentGrades.jsx` — uses **hardcoded mock data**
- No semester GPA progression chart
- No transcript view
- No enrollment validation engine (conflicts, prerequisites, capacity)
- No waitlist
- No academic structure selection UI (faculty → department → program → level)

### 4.2 Academic Structure Selection

**Scope Selection Enhancement:**
- Current: structural node + temporal scope from DomainContext/AcademicContext
- Enhancement: explicit "My Academic Program" section in student profile
- Show: Faculty → Department → Program → Level (as a breadcrumb)
- Allow change only during designated "Program Change" window
- Backend: `GET /student/academic-program`, `PUT /student/academic-program`

### 4.3 Course Catalog & Search (Overhaul)

**Full-text search:**
- Search by: course code, title, instructor, department, keywords
- Debounced input (300ms)
- Advanced filters: department, credits range, semester, day of week, time range, instructor
- Paginated results (20 per page)
- Sort by: code, title, credits, enrollment

**Course Card (enhanced):**

```
┌──────────────────────────────────────────┐
│ CS301  Software Engineering    ○ 3 cr    │
│ Instructor: Dr. Smith                    │
│ Schedule: MWF 9:00-10:15 | Room 301      │
│ Capacity: ████████░░ 28/30               │
│ Prerequisites: CS201, MATH201 ✓          │
│ ┌────────────────────────────────────┐   │
│ │         [Add to Cart]              │   │
│ └────────────────────────────────────┘   │
└──────────────────────────────────────────┘
```

**Course Detail Modal:**
- Full description
- Syllabus link
- Learning outcomes
- Assessment breakdown
- Textbook info
- Faculty evaluation ratings
- Similar courses

### 4.4 Enrollment Cart & Validation Engine

**Cart Architecture:**
- Zustand store for cart state
- Persisted to localStorage for session recovery
- Cart items: `{ offeringId, courseCode, courseTitle, credits, schedule, instructor }`

**Time-Slot Conflict Detection Algorithm:**

```js
function findTimeConflicts(cartItems, currentEnrollments) {
  const allSlots = [...currentEnrollments.flatMap(e => e.slots), ...cartItems.flatMap(c => c.slots)];
  const conflicts = [];

  for (let i = 0; i < allSlots.length; i++) {
    for (let j = i + 1; j < allSlots.length; j++) {
      if (slotsOverlap(allSlots[i], allSlots[j])) {
        conflicts.push({ slotA: allSlots[i], slotB: allSlots[j] });
      }
    }
  }

  return conflicts;
}

function slotsOverlap(a, b) {
  if (a.dayOfWeek !== b.dayOfWeek) return false;
  return a.startTime < b.endTime && b.startTime < a.endTime;
}
```

**Prerequisite Check:**
- Fetch student's completed courses from transcript
- For each cart item, check `prerequisites` array
- Display: ✓ met, ✗ not met, ⚠ currently enrolled (will satisfy if passed)
- Disable enrollment for unmet prerequisites

**Capacity Check:**
- Real-time seat availability via polling (every 30 seconds) or WebSocket
- Visual: capacity bar (green > 50%, yellow > 75%, red > 90%)
- Show waitlist count when full
- Auto-enable/disable "Add to Cart" based on availability

**Credit Limit Check:**
- Fetch student's max credit hours per semester (variable by program/academic standing)
- Show: "Enrolled: 12 | Cart: 6 | Max: 21 | Remaining: 3"
- Prevent adding courses that exceed max
- Warning if exceeding recommended load (>18 credits)

### 4.5 Registration Windows

**Registration Schedule Display:**
- Calendar-style view of registration periods
- Color-coded: Open, Closing Soon, Closed, Waitlist Only
- Countdown timer to next window
- Priority registration (honors students, special programs)

**Registration State Machine:**

```
Draft → Submitted → Pending Approval → Approved / Rejected
                    ↘ Cancelled (student-initiated)
```

**Bulk Operations:**
- Select all / deselect all
- Remove selected
- Save as draft (persist cart)
- Validate all (run all checks at once)
- Submit selected (enroll in checked items)

### 4.6 Waitlist Management

- Join waitlist when course is full
- Waitlist position display
- Auto-enroll when seat opens (with notification)
- Remove from waitlist
- Waitlist expiration (24 hours to accept offered seat)
- Weekly enrollment summary: "You moved up 3 positions on the CS301 waitlist"

### 4.7 Grade Display Enhancement

**Real Data Integration:**
- Replace mock `GRADE_DATA` with TanStack Query:

```js
const { data: grades } = useQuery({
  queryKey: ['student-grades', user?.id, activeScope],
  queryFn: () => studentService.fetchGrades(user.id, activeScope),
});
```

**Grade Visualization:**
- Cumulative GPA card (with trend arrow since last semester)
- GPA progression chart (bar chart: GPA per semester)
- Grade distribution pie chart (A, B, C, D, F)
- Course-by-semester tabbed view
- Grade details modal (per-course breakdown: midterm, assignments, final, total)

**Academic Standing Calculation:**
- Good: GPA ≥ 2.0
- Probation: GPA < 2.0 (first time)
- Suspension: GPA < 2.0 for 2 consecutive semesters
- Honor Roll: GPA ≥ 3.5
- Dean's List: GPA ≥ 3.7
- Display in profile snippet + grades page

### 4.8 Semester GPA Projection Calculator

- "What if" calculator: enter expected grades for in-progress courses
- Show projected GPA for current semester
- Show projected cumulative GPA
- Show impact of dropping a course (with W grade)
- Show what grades needed to achieve target GPA

---

## 🔷 PHASE 5: EVALUATION, HISTORY & DOCUMENT EXPORT

### 5.1 Full Transcript View

**Data:**
- TanStack Table (or custom table) with sortable, filterable columns
- Grouped by academic year → semester
- Per course: code, title, credits, grade, grade points, status
- Per semester: subtotal credits earned, semester GPA
- Cumulative: total credits earned, cumulative GPA

**Visual:**
- Expandable year → semester → course drilldown
- Color-coded grades (same as grades page)
- Official transcript formatting toggle
- GPA calculation verification (manual recompute to verify)

### 5.2 PDF Export Engine

**Requirements:**
- Client-side PDF generation (no server round-trip)
- Professional institutional formatting:
  - University logo + header
  - Student name, ID, National ID
  - Issue date, document number
  - Official watermark ("CAPITAL UNIVERSITY" diagonally)
  - UV-stamp placeholder
  - Digital signature line with hash
  - Footer: page X of Y, verification QR code

**Exportable Documents:**
1. **Official Transcript** — full academic history with GPA
2. **Grade Report** — current semester grades
3. **Weekly Schedule** — current timetable
4. **Financial Invoice** — fee breakdown
5. **Payment Receipt** — individual payment confirmation
6. **Enrollment Verification** — proof of enrollment letter
7. **Course Completion Certificate** — per-course certificate

**Technology Options:**
- `@react-pdf/renderer` (most flexible, full control)
- Or optimized `@media print` CSS stylesheet with `window.print()`
  - Hide navigation, sidebar, chrome
  - Show printable version with institutional formatting
  - Auto page breaks

**Implementation:**

```js
// core/services/exportService.js
export async function exportTranscript(studentId, options = {}) {
  const data = await fetchTranscriptData(studentId);
  const doc = (
    <PDFDocument>
      <TranscriptTemplate data={data} options={options} />
    </PDFDocument>
  );
  const blob = await pdf(doc).toBlob();
  saveAs(blob, `Transcript_${data.studentCode}_${new Date().toISOString().split('T')[0]}.pdf`);
}
```

### 5.3 Student Course History

- All courses taken, regardless of pass/fail
- Withdrawal history (W grades)
- Retake history (original grade → retake grade)
- Course repeat policy indicator
- Maximum attempt count

### 5.4 Degree Audit / Progress Tracker

**Academic Plan Progress:**
- Fetch student's academic plan (program requirements)
- Show progress bar: "You've completed 85/130 credit hours (65%)"
- Required courses: completed ✓, in-progress ○, remaining □
- Elective courses: completed, remaining slots
- GPA requirements: current vs minimum
- Capstone/Thesis requirements
- Internship credit hours

**Visual:**
- Progress wheel (circular progress)
- Category breakdown grid (Major, General Ed, Electives, Free)
- "What's Next" section: recommended courses for next semester
- Graduation eligibility indicator: "On track for 2028 graduation"
- If behind: "Suggestion: Take 18 credits next semester to stay on track"

---

## 🔷 PHASE 6: FINANCIALS & PAYMENT SYSTEM

### 6.1 Current State Assessment

- `StudentPaymentsPage.jsx` — comprehensive, real API integration, 4 tabs
- Uses invoice + payment services — good
- Missing: installment plan UI (tab exists but shows empty state), payment gateway integration, receipt download

### 6.2 Financial Dashboard Enhancement

**Summary Cards (enhanced):**
- Total Fees (current academic year)
- Paid Amount
- Outstanding Balance (with due date alert)
- Next Installment Amount + Due Date
- Late Payment Penalties (if any)
- Scholarship/Discount Applied

**Fee Structure Breakdown:**

```
Academic Year 2025-2026
├── Fall Semester
│   ├── Tuition (15 credits × EGP 1,200) = EGP 18,000
│   ├── Lab Fees = EGP 2,500
│   ├── Library Fee = EGP 500
│   ├── Student Activity Fee = EGP 300
│   └── Health Insurance = EGP 800
│   └── Total: EGP 22,100
├── Spring Semester (similar)
└── Total Academic Year: EGP 44,200
```

### 6.3 Installment Plans

- Default plan: 2 installments (pre-semester)
- Optional: 4 installments (quarterly) with processing fee
- Custom plan: request via service request
- Installment calendar view: "Next installment: EGP 11,050 due Aug 15, 2026"
- Late fee calculation: 1.5% per month on overdue

### 6.4 Payment Gateway Integration

**Supported Methods:**
- Credit/Debit Card (Visa, Mastercard, Meeza)
- Fawry (Egyptian e-payment network)
- Bank Transfer (with reference number)
- University Wallet (pre-paid account)
- Installment via partner banks

**Flow:**

```
Student clicks "Pay Now" → Amount + Invoice selected
  → POST /payments/create-session { invoiceId, amount, method }
  → Redirect to payment gateway (or embedded iframe)
  → On success: POST /payments/confirm { sessionId, transactionId }
  → UI: success toast, receipt download, invoice status update
  → On failure: error message, retry or alternative method
```

**Payment Receipt:**
- PDF download with transaction details
- Includes: transaction ID, date, amount, method, invoice reference
- University receipt header
- QR code for verification

### 6.5 Scholarship & Discount Display

- Active scholarships: name, amount, duration
- Discounts: early payment, sibling discount, financial aid
- Status: "Your Zakat Foundation Scholarship (EGP 15,000) covers 34% of tuition"
- Application link for financial aid

### 6.6 Financial History Archive

- Full transaction history (filter by date range, status, method)
- Export to CSV/Excel
- Annual financial summary
- Tax receipt generation (for eligible donations/fees)

---

## 🔷 PHASE 7: STUDENT SERVICES & REQUEST HUB

### 7.1 Current State Assessment

- Service catalog — functional (fetched from API)
- Request submission — functional
- Request list — functional with status filter
- Request details — functional
- Missing: Kanban/timeline view, push notifications, action-required transitions

### 7.2 Service Catalog Enhancement

**Service Card (enhanced):**

```
┌──────────────────────────────────────┐
│ 📄 ID Replacement Request            │
│ Get a replacement student ID card    │
│ ├─ Processing time: 3-5 business days│
│ ├─ Fee: EGP 150                     │
│ └─ Requirements: Police report ✓    │
│                                      │
│  Status: 🟢 Open     [Apply Now]    │
└──────────────────────────────────────┘
```

**Search & Filter:**
- Search by service name or keyword
- Filter by category (Academic, Financial, Administrative, Housing, Health)
- Filter by fee (free, paid)
- Sort by popularity, processing time, name

### 7.3 Request Status Tracking

**Kanban View:**

```
┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│  DRAFT   │ │ PENDING  │ │ IN REVIEW│ │COMPLETED │
│  (2)     │ │  (5)     │ │  (3)     │ │  (12)    │
├──────────┤ ├──────────┤ ├──────────┤ ├──────────┤
│ID Req #12│ │Transcript│ │Housing   │ │ID Req #8 │
│Transcript│ │ID Req #15│ │App #7    │ │Transcript│
└──────────┘ └──────────┘ └──────────┘ └──────────┘
```

**Timeline View:**

```
🟢 Application Submitted — Mar 1, 2026 09:15 AM
🟡 Under Review — Mar 2, 2026 10:30 AM
🔵 Additional Documents Requested — Mar 3, 2026 02:00 PM
   ⬜ [Upload Documents Button] ← Your Action Required
⚪ Awaiting Approval
```

### 7.4 Push Notification Integration

- WebSocket connection for real-time status updates
- Service Worker for push notifications (when tab not active)
- Notification types:
  - Request status change → "Your ID replacement request has been approved"
  - Payment due → "Tuition fee due in 7 days"
  - Registration window → "Course registration opens tomorrow"
  - Survey/action required → "Semester feedback form awaiting your response"
  - Waitlist update → "You moved up in CS301 waitlist"

### 7.5 Fee Calculation Integration

- Service request form shows fee upfront
- Fee calculated based on:
  - Service type
  - Urgency (standard vs express)
  - Quantity (e.g., transcript copies)
  - Delivery method (pickup vs courier)
- Pay during submission or generate invoice
- Receipt available after payment

---

## 🔷 PHASE 8: COMMUNICATION & NOTIFICATION ECOSYSTEM

### 8.1 In-App Notification Center

**Design:**
- List view with infinite scroll
- Filter: All, Unread, Read
- Sort: Newest, Oldest
- Group by date (Today, Yesterday, This Week, Older)
- Notification types with distinct icons:
  - 📌 Academic (grades, enrollment, schedule)
  - 💰 Financial (payments, invoices, scholarships)
  - 🛠️ Service (request updates)
  - 📢 Announcement (university-wide)
  - ⚠️ Alert (urgent, deadlines)
- Swipe-to-dismiss (mobile)
- Bulk actions: Mark all as read, Delete all

### 8.2 Real-Time Features

- WebSocket connection established after login
- Auto-reconnect with exponential backoff
- Events:
  - `notification:new` — new notification received
  - `request:status_change` — service request updated
  - `payment:confirmed` — payment processed
  - `registration:opened` — registration window opened
  - `seat:available` — waitlist->seat offered
  - `deadline:approaching` — 24h before deadline

### 8.3 University Announcements

- Announcement board widget on dashboard
- Pin important announcements (by admin)
- Archival system
- Read receipts (admin can see who read)
- Category: Academic, Administrative, Events, Emergency
- Emergency: red banner at top of ALL pages, cannot dismiss

### 8.4 Communication Hub (Future)

- Direct messaging with advisor
- Department-level announcement channels
- Course-specific discussion boards
- Anonymous feedback system

---

## 🔷 PHASE 9: ACADEMIC CALENDAR & SCHEDULE MANAGEMENT

### 9.1 Current State Assessment

- `StudentSchedule.jsx` — weekly timetable + monthly calendar
- Real API data with color coding
- Missing: full academic calendar, exam schedule, personal events

### 9.2 Academic Calendar View

**Full-Year View:**
- Selectable: Academic Year → Semester
- Month grid with events marked
- Event tooltip on hover
- Event types: Holidays, Exams, Registration Periods, Fee Deadlines, Breaks
- Legend for event types

**Event Details Modal:**
- Title, description, date/time, location
- Attached documents (e.g., exam schedule PDF)
- Add to personal calendar (Google Calendar / Outlook / ICS download)

### 9.3 Exam Schedule View

- Separate tab in schedule page
- Shows: date, time, course, room, seat number
- Conflicts highlighted (if any)
- Exam preparation: days remaining countdown
- Past exams: grade available indicator

### 9.4 Personal Schedule

- Student can add personal events (study sessions, appointments)
- Color-coded overlay on timetable
- Sync with external calendars (future)

### 9.5 Timetable Printing

- Print-friendly CSS for weekly timetable
- PDF export option
- Mobile-friendly compact view

---

## 🔷 PHASE 10: STUDENT PROFILE & PERSONALIZATION

### 10.1 Current State Assessment

- `StudentProfile.jsx` — basic edit form with personal + contact info
- Missing: profile picture upload, emergency contact, documents, academic info display

### 10.2 Profile Sections

**Personal Information:**
- Name (Arabic) [read-only]
- Name (English) [read-only]
- Date of Birth
- Gender
- Nationality
- National ID [display only]
- Religion (optional, for records)

**Contact Information:**
- Email (primary) + Email (secondary)
- Phone (mobile) + Phone (home)
- Permanent Address + Current Address
- Mailing preferences (email/SMS/push for which categories)

**Emergency Contact:**
- Name
- Relationship
- Phone (primary + secondary)
- Email
- Address

**Academic Information (read-only):**
- Student ID
- Faculty / Department / Program / Level
- Admission Year
- Expected Graduation Year
- Academic Advisor (name + email + office hours)
- Current GPA
- Academic Standing
- Enrolled Credits

**Guardian/Sponsor Information:**
- Name
- Relationship
- Phone
- Email
- Occupation
- Company (for sponsorship)

**Documents:**
- Upload: National ID copy, passport photo, birth certificate, high school certificate
- Upload status (pending, verified, rejected)
- Document expiry tracking

### 10.3 Profile Picture Upload

- Avatar click → upload modal
- Drag-and-drop zone
- Crop tool (circular crop to standard ID photo ratio)
- Max 5MB, JPG/PNG
- Preview before save
- Remove option

### 10.4 Settings & Preferences

**Account:**
- Change password (with strength meter)
- Language preference (Arabic/English)
- Theme (Light/Dark/System)

**Notifications:**
- Per-category toggle (Academic, Financial, Service, Announcements)
- Delivery channel (in-app, email, SMS)
- Quiet hours
- Digest frequency (real-time, daily, weekly)

**Privacy:**
- Profile visibility (to other students, to public)
- Directory listing opt-out
- Data sharing consent

### 10.5 QR Digital ID Card

- Generate digital student ID card
- QR code that links to verification endpoint
- Card shows: photo, name, ID, faculty, expiry
- Download as image or Apple Wallet/Google Pay pass

---

## 🔷 PHASE 11: ADMIN PORTAL ENHANCEMENT (CROSS-CUTTING)

### 11.1 Student Management

**Quick Overview:**
- Student count by faculty/department
- Active vs inactive
- New enrollments this semester
- At-risk students (GPA < 2.0)
- Graduation candidates

**Bulk Operations:**
- Bulk import via Excel/CSV (existing API, no UI)
- Bulk email/SMS
- Bulk status update (activate/deactivate)
- Bulk program assignment

### 11.2 Academic Administration

**Course Management:**
- Course catalog CRUD
- Prerequisite mapping with graph visualization
- Course offering scheduling with room assignment
- Instructor assignment with workload check

**Program Management:**
- Program creation with required/elective course mapping
- Degree audit rule configuration
- Credit hour requirements per program

**Registration Management:**
- Override enrollment (force-add student to full course)
- Drop course (with W or WF grade)
- Registration window configuration
- Waitlist management (approve/monitor)

### 11.3 Financial Administration

- Global invoice listing (new backend endpoint needed)
- Payment transaction ledger
- Manual invoice creation
- Fee structure configuration per program/semester
- Scholarship/discount management
- Refund processing

### 11.4 Service Request Administration

- Request queue with assignment
- Workflow designer (drag-and-drop step configuration)
- Automated notifications on status change
- SLA tracking and breach alerts
- Reporting dashboard (volume, avg resolution time, satisfaction)

---

## 🔷 PHASE 12: PERFORMANCE, TESTING & QUALITY

### 12.1 Performance Optimization

**Code Splitting:**
- Route-based splitting (already using `React.lazy` — verify all routes)
- Component-level splitting for heavy components (PDF viewer, calendar, drag-and-drop)
- Library chunking (vendor, Radix, TanStack, etc.)

**Memoization:**
- `React.memo` on all card components, table rows, widgets
- `useMemo` for expensive computations (conflict detection, GPA calc)
- `useCallback` for event handlers passed to children

**Virtualization:**
- Use `react-window` or `@tanstack/virtual` for large lists (course catalog, transcripts, notifications)
- Infinite scroll for notification list

**Image Optimization:**
- WebP format for university images
- Lazy loading with `loading="lazy"`
- Responsive image sizes

**Bundle Analysis:**
- `vite-plugin-visualizer` for bundle visualization
- Target: initial JS < 200KB, initial CSS < 50KB
- Tree-shaking audit

### 12.2 Testing Strategy

**Unit Tests (Vitest):**
- Test all utility functions (conflict detection, validation, formatting)
- Test custom hooks with `renderHook`
- Test Zustand stores
- Test i18n keys exist for all translations
- **Target:** 80%+ coverage on utility code

**Component Tests (@testing-library/react):**
- Test all reusable components (DataTable, Skeleton, Toast, etc.)
- Test all page components (happy path + loading + error + empty)
- Test form validation
- Test route guards and permission gates
- **Target:** 70%+ coverage on components

**Integration Tests:**
- Test auth flow (login → dashboard)
- Test course enrollment flow (search → add to cart → validate → enroll)
- Test service request flow (browse → apply → track)
- Test payment flow (view invoices → pay → receipt)
- Use MSW for API mocking

**E2E Tests (Playwright or Cypress):**
- Full user journeys
- Cross-browser testing (Chrome, Firefox, Safari, Edge)
- Mobile viewport testing
- RTL/Arabic layout testing
- **Target:** Critical paths covered

### 12.3 Accessibility (a11y)

**Audit & Remediation:**
- WCAG 2.1 AA compliance target
- Semantic HTML: `<nav>`, `<main>`, `<section>`, `<article>`, `<aside>`, `<header>`, `<footer>`
- ARIA landmarks on all pages
- Keyboard navigation: Tab order, focus indicators, skip-to-content link
- Screen reader: proper `aria-label`, `aria-describedby`, `role` attributes
- Color contrast: all text meets 4.5:1 ratio
- Focus trap in modals (using `focus-trap-react` — already in deps)
- Reduced motion media query for animations

**Testing:**
- Automated axe-core testing in CI
- Manual screen reader testing (NVDA, VoiceOver)
- Keyboard-only navigation testing

### 12.4 Error Handling

**Error Boundaries:**
- Module-level ErrorBoundary for each route
- Dashboard-level ErrorBoundary as fallback
- Global unhandled rejection handler
- Error logging to console + optional Sentry integration

**Error States:**
- Network error: retry button, offline indicator
- Server error: "Something went wrong" with error ID
- Permission error: "You don't have access" with contact support
- Validation error: inline field errors with zod
- Rate limit: "Too many requests, please wait"

**Graceful Degradation:**
- Feature flags for experimental features
- Fallback UI when module fails to load
- Offline-aware: cached data display, queue actions

---

## 🔷 PHASE 13: RTL & INTERNATIONALIZATION DEEP DIVE

### 13.1 Current State

- Arabic as fallback language (`fallbackLng: 'ar'`)
- 12 JSON files per language (common, auth, navigation, dashboard, landing, students, staff, structure, studentServices, notifications, permissions, validation)
- RTL direction switching via `document.documentElement.dir`
- `body.rtl` class toggle

### 13.2 Enhancements

**Complete i18n of all strings:**
- Audit all `.jsx` files for hardcoded English strings
- All user-facing text → `t('key')` pattern
- Dynamic variables via `t('key', { variable })`
- Plural support: `t('courses', { count: courses.length })`
- Date/time formatting: `Intl.DateTimeFormat` with `i18n.language`
- Number/currency formatting: `Intl.NumberFormat`

**RTL CSS Audit:**
- Replace all `left`/`right` with `inset-inline-start`/`inset-inline-end`
- Replace `margin-left`/`margin-right` with `margin-inline-start`/`margin-inline-end`
- Replace `padding-left`/`padding-right` with `padding-inline-start`/`padding-inline-end`
- Replace `border-left`/`border-right` with `border-inline-start`/`border-inline-end`
- Text alignment: use `text-align: start` / `end` instead of `left` / `right`
- Flexbox: avoid `flex-direction` row-reverse hacks for RTL; use proper logical properties
- Icons: flip icons that indicate direction (arrows, chevrons) with `transform: scaleX(-1)` in RTL
- Toast: slide in from `inline-end` (right in LTR, left in RTL)

**Language-Specific Formatting:**
- Arabic-Indic digits (٠١٢٣٤٥٦٧٨٩) vs Western digits (0123456789)
- Arabic date format: `٢٥ رجب ١٤٤٧` vs `10 June 2026`
- Currency: ج.م. ١٬٢٠٠ vs EGP 1,200

### 13.3 Translation Workflow

- Extract all keys to a single source of truth
- Key naming convention: `category.subcategory.action` — e.g., `course.catalog.search` instead of `search_courses`
- Translation management system (optional: Crowdin / Lokalise integration)
- Fallback strategy: key → English translation → display key as fallback
- Missing key detection in development mode (console warning)

---

## 🔷 PHASE 14: INFRASTRUCTURE & DEVOPS

### 14.1 Docker & Containerization

- Frontend Dockerfile (multi-stage build + nginx serving)
- docker-compose with: frontend, backend, SQL Server, Redis, MongoDB
- Health check endpoints
- Volume mounts for persistent data
- Environment variable injection

### 14.2 CI/CD Pipeline

**GitHub Actions:**
- Lint → Test → Build → Deploy
- Preview deployments for PRs
- Sentry release tracking
- Automated accessibility audit (pa11y/axe)
- Bundle size regression check
- E2E test run

**Environment Strategy:**
- `development` — auto-deploy on PR merge to develop
- `staging` — manual promote, mirrors production
- `production` — approval gate, blue-green deployment

### 14.3 Monitoring & Analytics

- Error tracking: Sentry
- Performance monitoring: Web Vitals (LCP, FID, CLS)
- Usage analytics: page views, feature usage (opt-in)
- Server health: uptime, response times, error rates
- User feedback: in-app "Report a Problem" button

---

## 🔷 PHASE 15: NEW MODULES (ADDITIONAL FEATURES)

### 15.1 Library Integration

- Search library catalog
- Currently borrowed books with due dates
- Overdue fines display
- Book reservation/renewal
- Digital resources access (e-journals, databases)
- Library announcements

### 15.2 Exam & Assessment Portal

- Exam timetable with seat assignment
- Midterm/quiz schedule
- Grade release notifications
- Exam results breakdown (per question/topic)
- Grade appeal submission

### 15.3 Attendance Tracking

- Student attendance record per course
- Absence count with warning thresholds
- Excused absence request
- Attendance statistics chart

### 15.4 Internship & Career Services

- Internship opportunities listing
- Apply for internship
- CV/resume upload
- Career counseling appointments
- Employer networking events
- Alumni mentorship program

### 15.5 Housing & Transportation

- Housing application
- Room assignment status
- Dormitory rules and regulations
- Maintenance request
- Bus route schedules
- Transportation card top-up

### 15.6 Health Services

- Clinic appointment booking
- Health insurance status
- Medical records (immunization, checkups)
- Sick leave request
- Mental health resources
- Emergency contact

### 15.7 Clubs & Extracurricular

- Club listings with join request
- Event calendar
- Volunteer opportunities
- Student government elections
- Achievement/awards tracking

### 15.8 Alumni Transition

- Graduation application checklist
- Alumni directory
- Transcript archive
- Alumni benefits
- Donation platform
- Continuing education opportunities

---

## 🔷 UI/UX DESIGN SYSTEM EXTENSION

### 16.1 Component Library Standardization

**Existing Components to Enhance:**

| Component | Enhancement |
|-----------|-------------|
| `Skeleton.jsx` | Add SkeletonTable, SkeletonCard, SkeletonText variants; shimmer animation |
| `DataTable.jsx` | Add row expansion, inline editing, column reorder, export to CSV |
| `EmptyState.jsx` | Add illustration variants per context (no data, no results, no access, error) |
| `ErrorBoundary.jsx` | Add error reporting button, retry callback, fallback component prop |
| `StatusBadge.jsx` | Add animated pulse for "in-progress" status, dot indicator variant |
| `Breadcrumbs.jsx` | Add schema.org JSON-LD structured data for SEO |
| `CommandPalette.jsx` | Add keyboard shortcut hints, recent searches, fuzzy search |
| `ConfirmDialog.jsx` | Add destructive variant (red), checkbox "Don't ask again" |
| `Drawer.jsx` | Add stacked drawers, responsive behavior |

**New Components to Create:**

| Component | Description |
|-----------|-------------|
| `PageHeader` | Consistent page header with title, subtitle, actions, breadcrumbs |
| `StatCard` | Metric display with icon, trend indicator, sparkline |
| `Timeline` | Vertical/horizontal timeline for request tracking |
| `KanbanBoard` | Drag-and-drop kanban columns with card rendering |
| `Calendar` | Month/week/day views with event rendering |
| `FileUpload` | Drag-and-drop file upload with progress, preview, remove |
| `ColorPicker` | Simple color picker (for personalization settings) |
| `Avatar` | Avatar with initials fallback, online indicator, size variants |
| `SearchInput` | Debounced search with clear button, results count |
| `FilterPanel` | Multi-filter panel with apply/clear, active filter badges |
| `ProgressWheel` | Circular progress with label (for degree progress) |
| `Chart` | Simple SVG chart (bar, line, pie) without heavy library dependency |
| `Rating` | Star rating component (for course feedback) |
| `Signature` | Digital signature pad component |
| `QRCode` | QR code generation for ID card, document verification |

### 16.2 Responsive Design Breakpoints

```css
/* Mobile: < 768px */
/* Tablet: 768px - 1024px */
/* Desktop: > 1024px */
/* Wide: > 1440px */
```

- Mobile: single column, hamburger menu, bottom navigation bar
- Tablet: 2-column dashboard, collapsible sidebar (icon-only mode)
- Desktop: full layout with sidebar + optional secondary sidebar
- Wide: max-width container, multi-column dashboards

### 16.3 Dark Mode

- CSS custom properties with `.dark` class
- System preference detection via `prefers-color-scheme`
- Manual toggle in user settings
- Persisted choice in localStorage
- Smooth transition on theme change

### 16.4 Animation Philosophy

- Purposeful, not decorative
- Duration: 200-350ms for UI transitions, 500-1000ms for entrance animations
- Easing: cubic-bezier for natural motion
- `prefers-reduced-motion: reduce` → disable all animations
- Supported animations:
  - Page transitions (fade)
  - Card mount (fade + slide-up)
  - Modal/drawer (slide + fade overlay)
  - Skeleton shimmer
  - Number counter
  - Progress bar fill
  - Toast slide-in
  - Hover scale on interactive cards

---

## 🔷 MIGRATION ROADMAP: JAVASCRIPT → TYPESCRIPT

### Phase-Gated Approach:

**Phase A (Immediate): JSDoc Annotations**
- Add `// @ts-check` to critical files
- Add JSDoc type annotations to all service functions, hooks, stores
- Create `types.js` files with `@typedef` definitions for major entities

**Phase B: Shared Types**
- Convert `core/services/*` to `.ts`
- Convert `core/api/apiClient.js` to `.ts`
- Convert `core/stores/*` to `.ts`
- Convert all hooks to `.ts`
- Create full TypeScript interfaces for all backend DTOs

**Phase C: Component Migration**
- Convert core components (`core/components/*`) to `.tsx`
- Convert utility files to `.ts`
- Convert layout files to `.tsx`

**Phase D: Module Migration**
- Convert feature modules one at a time
- Priority: studentPortal → admin → studentServices → remaining

### TypeScript Configuration:

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "jsx": "react-jsx",
    "jsxImportSource": "react",
    "paths": {
      "@/*": ["./src/*"]
    },
    "baseUrl": ".",
    "allowJs": true,
    "checkJs": false
  }
}
```

---

## 🔷 BACKEND GAPS TO ADDRESS

Based on `backend-requirements.md` audit:

| Gap | Priority | Impact |
|-----|----------|--------|
| Missing global invoice list endpoint (admin finance dashboard) | **High** | Blocks admin finance dashboard |
| Missing global payment transaction ledger | **High** | Blocks admin finance dashboard |
| No "mark-all-read" for notifications (currently fans out per-notification) | **Medium** | Performance issue with 50+ notifs |
| `/api/Notifications` casing inconsistency | **Low** | Cosmetic, works but should follow kebab-case |
| Profile records `VerifiedBy` returns raw GUID with no name | **Medium** | UX: shows GUID instead of "Dr. Ahmed Ali" |
| Permission tree `IsAssigned` not populated on full-tree call | **Medium** | Only works on role-context call |
| Bulk import endpoints exist no UI | **Low** | Future enhancement |

### Additional Backend Requirements:

| Requirement | Priority |
|-------------|----------|
| `POST /auth/change-password` — wire frontend `ChangePasswordModal` | **High** |
| `POST /auth/forgot-password` — wire frontend `ForgotPasswordModal` | **High** |
| `POST /auth/refresh` — ensure `passwordExpiryDate` in response | **High** |
| `GET /student/profile/completeness` — profile completeness endpoint | **High** |
| `GET /student/mandatory-actions` — mandatory actions endpoint | **High** |
| `POST /student/mandatory-actions/{id}/submit` — submit mandatory action | **High** |
| `POST /student/mandatory-actions/{id}/acknowledge` — acknowledge document | **High** |
| `GET /student/transcript` — full transcript data | **High** |
| `GET /student/degree-audit` — degree progress data | **Medium** |
| `GET /student/payment-methods` — available payment methods | **Medium** |
| `POST /payments/create-session` — payment gateway session | **Medium** |
| `POST /payments/confirm` — payment confirmation | **Medium** |
| `GET /student/academic-program` — current academic program | **Medium** |
| `PUT /student/academic-program` — update academic program (during window) | **Medium** |
| WebSocket endpoint for real-time notifications | **Medium** |
| `GET /student/upcoming-exams` — exam schedule | **Medium** |
| `POST /student/profile/upload-photo` — avatar upload | **Low** |
| `GET /student/library/borrowed` — library integration | **Low** |
| `GET /student/attendance` — attendance records | **Low** |

---

## 🔷 IMPLEMENTATION ORDER & DEPENDENCIES

```
Phase 0: Foundation & Cleanup
  └── No dependencies

Phase 1: Auth Enhancement
  └── Depends on: Phase 0

Phase 2: Blocker System (Middleware)
  └── Depends on: Phase 1 (new auth endpoints)

Phase 3: Dashboard Overhaul
  └── Depends on: Phase 1, Phase 2

Phase 4: Course Enrollment Engine
  └── Depends on: Phase 1, Phase 3 (scope system)

Phase 5: Grades & Transcript
  └── Depends on: Phase 4

Phase 6: Financials Enhancement
  └── Depends on: Phase 1, Phase 3

Phase 7: Services Enhancement
  └── Depends on: Phase 1, Phase 3

Phase 8: Notifications & Real-time
  └── Depends on: Phase 1, Phase 3, Phase 7

Phase 9: Calendar & Schedule
  └── Depends on: Phase 4

Phase 10: Profile & Personalization
  └── Depends on: Phase 2

Phase 11: Admin Enhancement
  └── Depends on: Phase 6, Phase 7

Phase 12: Performance & Testing
  └── Can run in parallel with any phase

Phase 13: i18n/RTL Deep Dive
  └── Can run in parallel with any phase

Phase 14: DevOps
  └── Depends on: minimum viable product

Phase 15: New Modules
  └── Depends on: core stability
```

---

## 🔷 SUCCESS METRICS & ACCEPTANCE CRITERIA

### Functionality:

- [ ] Student can login with National ID + password
- [ ] First-time login forces password change
- [ ] Expired password forces password change
- [ ] Profile completeness check blocks navigation until complete
- [ ] Mandatory actions block navigation until resolved
- [ ] Dashboard shows personalized data from real API
- [ ] Course catalog loads from API with search/filter
- [ ] Enrollment cart validates time conflicts, prerequisites, capacity
- [ ] Grades load from API with GPA calculation
- [ ] Transcript loads and can be exported as PDF
- [ ] Financial ledger shows real invoice data
- [ ] Payment gateway integration processes payment
- [ ] Services catalog loads from API
- [ ] Request submission creates request on backend
- [ ] Real-time notifications arrive via WebSocket

### Performance:

- [ ] Initial page load < 2 seconds (3G)
- [ ] Dashboard renders in < 500ms (cached)
- [ ] Course search returns results in < 300ms
- [ ] Time conflict detection completes in < 100ms
- [ ] PDF export completes in < 5 seconds
- [ ] Lighthouse score > 90 for Performance, Accessibility, Best Practices

### Quality:

- [ ] Unit test coverage > 70%
- [ ] E2E tests cover all critical paths
- [ ] WCAG 2.1 AA compliance
- [ ] No console errors in any state
- [ ] Error boundaries catch all errors gracefully
- [ ] All text is i18n'd (no hardcoded strings)
- [ ] RTL layout is pixel-perfect in Arabic

### UX:

- [ ] All async operations show skeleton loaders
- [ ] All errors show actionable feedback
- [ ] Empty states show helpful messages
- [ ] Mobile responsive at all breakpoints
- [ ] Keyboard navigable end-to-end
- [ ] Screen reader compatible
- [ ] Dark mode available and consistent

---

## 🔷 FINAL DIRECTIVE

This specification represents the complete, authoritative blueprint for the Capital University Student Portal. Every module, component, and interaction has been designed with the following priorities:

1. **Student Experience First** — every feature should make the student's academic life easier, faster, and more transparent
2. **Data Integrity Always** — validation, confirmation, and safeguards prevent errors before they happen
3. **Performance is a Feature** — speed and responsiveness are non-negotiable
4. **Accessibility for All** — the portal must work for every student regardless of ability
5. **Internationalization by Default** — Arabic-first with seamless English support
6. **Professional Presentation** — the portal reflects the university's brand and standards

**Begin execution with Phase 0 (Foundation & Cleanup), then proceed sequentially through Phase 1 (Auth Enhancement). Each phase must be fully tested, documented, and verified before moving to the next.**

---

*End of Specification — ~850+ detailed requirements across 15+ phases and 30+ subsections*
