# CAPU Portal Frontend - Quick Reference Guide

## 1. ROUTING SETUP

### Main Router
**File:** `src/core/router/AppRouter.jsx`

**Route Structure:**
- `/` → Landing Page (public)
- `/admin/login` → Admin Login
- `/admin/dashboard` → Protected: Admin Dashboard
- `/admin/users/*` → Protected: User Management (CRUD)
- `/admin/university-structure` → Protected: Org Structure
- `/admin/permissions` → Protected: Permissions Matrix
- `/admin/roles` → Protected: Roles Management
- `/admin/staff` → Protected: Staff Directory
- `/admin/students` → Protected: Student Directory

**Key Files:**
- `AppRouter.jsx` - Route definitions
- `routeRegistry.js` - Dynamic component mapping
- `RouteGuard.jsx` - Permission-based route protection

---

## 2. COMPONENT ARCHITECTURE

### Core Structure
```
core/
├── api/apiClient.js         → Axios with interceptors
├── auth/                    → Authentication & authorization
├── contexts/                → Global state (React Context)
├── layouts/                 → DashboardLayout wrapper
├── navigation/              → Navbar & Sidebar components
├── router/                  → Routing config
├── services/                → API service layer
└── manifests/               → Module manifest loader
```

### Modules (7 Total)
1. **admin** - Dashboard & overview
2. **users** - User management (students/staff)
3. **university** - Org structure tree
4. **landing** - Public landing page
5. **permissions** - Roles & permissions
6. **staff** - Staff directory
7. **students** - Student directory

### Key Components

**Layout:**
- `DashboardLayout` - Main wrapper with sidebar/navbar

**Navigation:**
- `Navbar` - Top bar with scope/year/semester selectors
- `Sidebar` - Left menu with collapsible categories

**Auth:**
- `LoginLayout` - Login page wrapper
- `LoginForm` - Reusable form component
- `ForgotPasswordModal` - Password reset

---

## 3. STYLING METHODOLOGY

### Approach: Custom CSS + CSS Variables
**No Tailwind, CSS Modules, or pre-built UI libraries**

### CSS Variables (in `index.css`)
```css
--navy-primary: #1a1f5e
--navy-accent: #2e3591
--gold: #c9a84c
--gold-light: #e0c06a
--navbar-height: 72px
--sidebar-width: 280px
--heading: "Space Mono", monospace
--body-font: "DM Sans", sans-serif
```

### Breakpoints
- Mobile: < 768px
- Tablet: < 1024px
- Desktop: >= 1024px

### File Organization
```
core/styles/
├── navbar.css
├── sidebar.css
└── (auth styles in auth/styles/)

modules/{module}/styles/
├── {module}.css
└── (component-specific CSS)
```

---

## 4. API CLIENT CONFIGURATION

### Axios Setup (`core/api/apiClient.js`)

**Configuration:**
- Base URL: `VITE_API_BASE_URL` or `http://localhost:5256`
- Content-Type: `application/json`

**Interceptors:**
- Request: Adds Bearer token from localStorage
- Response: Handles 401 errors, triggers logout

**Methods:**
- `getToken()` - Get access token
- `clearTokens()` - Clear stored tokens
- `setOnUnauthorized(callback)` - Set logout callback

### Services Layer
```
core/services/
├── studentService.js
├── staffService.js
├── academicService.js
├── permissionService.js
└── structureService.js

modules/{module}/services/
└── {module}Service.js
```

---

## 5. STATE MANAGEMENT

### Architecture: React Context API (No Redux)

### Global Contexts

| Context | Purpose | State |
|---------|---------|-------|
| **AuthContext** | Authentication & user | user, permissions, isAuthenticated |
| **PermissionContext** | Authorization levels | permissions map, can() method |
| **DomainContext** | Selected organization | selectedDomain |
| **AcademicContext** | Academic year/semester | selectedYear, selectedSemester |
| **ScopeContext** | Global scope selection | selectedScope (localStorage) |

### Provider Nesting (main.jsx)
```javascript
<BrowserRouter>
  <AuthProvider>
    <PermissionProvider>
      <DomainProvider>
        <AcademicProvider>
          <App />
        </AcademicProvider>
      </DomainProvider>
    </PermissionProvider>
  </AuthProvider>
</BrowserRouter>
```

### Custom Hooks
- `useAuth()` - Get auth context
- `usePermission()` - Get permission context
- `useDomain()` - Get domain context
- `useAcademic()` - Get academic context
- `useScope()` - Get scope context
- `useUsers()` - User management
- `useUniversityStructure()` - Org structure

---

## 6. PAGES & MODULES

### Admin Module
- **Page:** AdminDashboard
- **Route:** `/admin/dashboard`
- **Permission:** dashboard.dashboard.view
- **Components:** DashboardHeader, StatsCards, RecentActivities, QuickActions

### Users Module
- **Routes:**
  - `/admin/users` → UserManagement (students/staff tabs)
  - `/admin/users/add-student` → AddStudent form
  - `/admin/users/edit-student/:id` → EditStudent form
  - `/admin/users/add-staff` → AddStaff form
  - `/admin/users/edit-staff/:id` → EditStaff form
  - `/admin/users/:id` → UserDetails view
- **Hook:** useUsers() - Manages state & API calls
- **Tables:** StudentTable, StaffTable
- **Filters:** Search, status, role, faculty, program, level

### University Module
- **Page:** UniversityStructurePage
- **Route:** `/admin/university-structure`
- **Permission:** structure.structure.view/insert/editclose/delete
- **Features:** Tree view, CRUD modals, drag-drop reorder, scope selection

### Landing Module
- **Page:** LandingPage
- **Route:** `/`
- **No Auth Required**
- **Components:** HeroSlider, StatsSection, FacultiesSection, NewsSection

### Permissions Module
- **Routes:** `/admin/permissions`, `/admin/roles`
- **Menu:** Security & Access category
- **Permission:** permissions.permissions.*

### Staff Module
- **Route:** `/admin/staff`
- **Menu:** People Management > Staff Directory
- **Permission:** staff.directory.*

### Students Module
- **Route:** `/admin/students`
- **Menu:** People Management > Student Directory
- **Permission:** students.directory.*

---

## 7. AUTHENTICATION & AUTHORIZATION

### Permission Levels
```
Level 0: NONE (no access)
Level 1: VIEW (read-only)
Level 2: INSERT (create)
Level 3: EDIT_CLOSE (update & close)
Level 4: OPEN (reopen/reverse actions)
Level 5: DELETE (remove)
```

### Permission Format
`resource.action.level`

**Examples:**
- `users.users.view` - Can view users
- `users.users.insert` - Can create users
- `structure.structure.editclose` - Can edit org structure
- `permissions.permissions.delete` - Can delete permissions

### Usage
```javascript
const { can, getLevel } = usePermission();

if (can('users.users.view')) {
  // Show users management
}

const level = getLevel('structure.structure.view');
if (level >= 3) {
  // Can edit
}
```

### Route Protection
```javascript
<Route element={<RouteGuard resource="users.users.view" minLevel={1} />}>
  <Route path="/admin/users" element={<UserManagement />} />
</Route>
```

---

## 8. UI COMPONENT LIBRARIES

### External Dependencies
- **Lucide React** (v1.14.0) - Icons (300+)
- **React Router DOM** (v7.15.0) - Routing
- **Axios** (v1.16.1) - HTTP client
- **React** (v19.2.6) - UI library
- **React DOM** (v19.2.6) - DOM rendering

### Design System (No Pre-Built UI Library)

**Colors:**
- Primary: #1a1f5e (Navy)
- Accent: #2e3591 (Navy Accent)
- Gold: #c9a84c
- Light Gold: #e0c06a

**Typography:**
- Headings: Space Mono
- Body: DM Sans
- Code: Space Mono

**Spacing:**
- Navbar Height: 72px
- Sidebar Width: 280px
- Sidebar Collapse Width: 64px

---

## 9. MANIFEST SYSTEM

### Module Manifest Structure
```json
{
  "id": "module-id",
  "name": "Module Name",
  "version": "1.0.0",
  "routes": [
    {
      "path": "/path",
      "component": "ComponentName",
      "permission": "resource.action.level",
      "moduleId": "module-id"
    }
  ],
  "menuItems": [
    {
      "category": "Category",
      "label": "Menu Label",
      "path": "/path",
      "icon": "IconName",
      "permission": "resource.action.level"
    }
  ],
  "permissions": {
    "resources": ["resource.action.level", ...]
  }
}
```

### Manifest Loading
- **File:** `core/manifests/manifestLoader.js`
- **Method:** `getAllRoutes()` - Returns all module routes
- **Method:** `getAllMenuItems()` - Returns all menu items
- **Method:** `getGroupedMenuItems()` - R
