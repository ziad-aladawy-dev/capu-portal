# CAPU Portal - Frontend Architecture Report

**Generated:** May 19, 2026

---

## Table of Contents
1. [Project Overview](#project-overview)
2. [Routing Setup](#routing-setup)
3. [Component Architecture](#component-architecture)
4. [Styling Methodology](#styling-methodology)
5. [API Client Configuration](#api-client-configuration)
6. [State Management](#state-management)
7. [Pages & Modules](#pages--modules)
8. [UI Component Libraries](#ui-component-libraries)
9. [Directory Structure Tree](#directory-structure-tree)

---

## Project Overview

**Frontend Stack:**
- React 19.2.6 with React Router DOM 7.15.0
- Vite 8.0.12 as build tool
- Axios 1.16.1 for HTTP requests
- Lucide React 1.14.0 for icons
- Vite Plugin Federation for module federation support

**Build Configuration:**
- Module Bundler: Vite with ESNext target
- Plugin Federation enabled for micro-frontend architecture
- React Compiler (babel-plugin-react-compiler) for performance optimization
- CSS Code Splitting disabled for better control

---

## 1. Routing Setup

### Router Architecture

**Main Router File:** `frontend/src/core/router/AppRouter.jsx`

#### Route Structure:
```
/ (Landing Page)
├── /admin/login (Admin/Staff Login)
├── /student/login (Student Login)
└── /admin/* (Protected Dashboard Routes)
    ├── /admin/dashboard
    ├── /admin/users (User Management)
    │   ├── /admin/users/add-student
    │   ├── /admin/users/edit-student/:id
    │   ├── /admin/users/add-staff
    │   ├── /admin/users/edit-staff/:id
    │   └── /admin/users/:id (User Details)
    ├── /admin/programs
    ├── /admin/permissions
    ├── /admin/sync
    └── /admin/university-structure
```

### Route Guard & Protection

**Implementation:**
- `RouteGuard.jsx` - Wraps protected routes with permission checks
- `AppRouter.jsx` - Entry point managing route definitions
- `routeRegistry.js` - Component mapping and route metadata
- Permission-based route access control using `minLevel: 1`

**Route Registry System:**
- Dynamic route registration via manifest files
- Component mapping through `COMPONENT_MAP` object
- Placeholder components for undefined routes
- Support for Navigate components for redirects

### Dynamic Route Loading

**Manifest-Based Routing:**
Routes are loaded from module manifests (JSON files):
```
modules/{module}/manifest.json
├── routes: Route definitions with components and permissions
├── menuItems: Sidebar navigation items
└── permissions: Resources and permission levels
```

---

## 2. Component Architecture

### Directory Structure

```
src/
├── core/
│   ├── api/                    # API client configuration
│   ├── auth/                   # Authentication & authorization
│   │   ├── pages/             # Login pages
│   │   ├── components/        # Auth-related components
│   │   ├── AuthContext.jsx    # Main auth state
│   │   ├── PermissionContext.jsx
│   │   ├── RouteGuard.jsx
│   │   ├── useAuth.js
│   │   └── usePermission.js
│   ├── contexts/              # Global React contexts
│   ├── layouts/               # Layout components (DashboardLayout)
│   ├── navigation/            # Navigation components
│   │   ├── navbar/           # Top navigation bar
│   │   ├── sidebar/          # Left sidebar
│   │   └── secondarySidebar/
│   ├── router/               # Routing configuration
│   ├── services/             # API service layer
│   ├── manifests/            # Module manifest loader
│   ├── styles/               # Core CSS files
│   └── components/           # Shared UI components (if any)
└── modules/
    ├── admin/                # Admin Dashboard module
    ├── landing/              # Landing page module
    ├── permissions/          # Permissions & Roles module
    ├── staff/                # Staff Directory module
    ├── students/             # Student Directory module
    ├── university/           # University Structure module
    └── users/                # User Management module
```

### Core Components

#### Layout Components
1. **DashboardLayout** (`core/layouts/DashboardLayout.jsx`)
   - Main layout wrapper for authenticated pages
   - Manages sidebar and navbar
   - Responsive design with mobile breakpoint at 768px
   - Sidebar toggle functionality
   - Smooth transitions for content margin

#### Navigation Components
1. **Navbar** (`core/navigation/navbar/Navbar.jsx`)
   - Top navigation bar with:
     - Sidebar toggle button
     - Scope selector (Domain/Building dropdown)
     - Academic year selector
     - Semester selector
     - Search bar
     - Notifications bell
     - Help icon
     - User avatar
   - Dropdown menus with click-outside detection
   - Integration with DomainContext and AcademicContext

2. **Sidebar** (`core/navigation/sidebar/Sidebar.jsx`)
   - Left navigation sidebar
   - Collapsible category groups
   - Icon-based category headers
   - Permission-based menu item filtering
   - User card with avatar and role
   - Logout button
   - Responsive mobile drawer behavior
   - Menu structure built from manifest files

3. **SecondrarySidebar** (`core/navigation/secondarySidebar/SecondarySidebar.jsx`)
   - Additional sidebar for supplementary navigation

#### Auth Components
1. **LoginLayout** - Wrapper for login pages
2. **LoginForm** - Reusable login form component
3. **ForgotPasswordModal** - Password reset modal
4. **UniversityLogo** - University branding component

### Module Components

#### Admin Module (`modules/admin/`)
Components:
- DashboardHeader
- DashboardSearch
- StatsCards
- RecentActivities
- QuickActions

Page:
- AdminDashboard

#### Users Module (`modules/users/`)
Components:
- FacultyPageHeader
- UserFilters
- UserStats
- StudentTable
- StaffTable
- ErrorMessage
- LoadingSpinner
- userTypeConfig

Pages:
- UserManagement
- AddStudent / EditStudent
- AddStaff / EditStaff
- UserDetails

Hooks:
- useUsers - Central hook for user management

#### University Module (`modules/university/`)
Components:
- TreeNode
- AddEditNodeModal
- ConfirmDeleteModal
- MoveNodeModal
- ScopeTreeModal

Page:
- UniversityStructurePage

Hooks:
- useUniversityStructure

Services:
- universityStructureService

Utils:
- nodeTypeHelpers
- treeSearch
- treeUtils

#### Landing Module (`modules/landing/`)
Components:
- LandingNavbar
- HeroSlider
- StatsSection
- FacultiesSection
- ServicesSection
- NewsSection
- CTASection
- Reveal (animation component)
- CountUp (counter animation)

Data:
- landingData.js

Page:
- LandingPage

#### Staff & Students Modules
Minimal structure with:
- Directory pages
- Simple list/table views

#### Permissions Module (`modules/permissions/`)
Pages:
- PermissionsPage
- RolesPage

---

## 3. Styling Methodology

### CSS Architecture

**Approach:** Custom CSS with CSS Variables and modular organization

### CSS Variables (in `src/index.css`)

**Color Scheme:**
```css
--navy-primary: #1a1f5e
--navy-accent: #2e3591
--gold: #c9a84c
--gold-light: #e0c06a
--text-h: #1a1f5e
--code-bg: #eef3fb
```

**Layout:**
```css
--navbar-height: 72px
--sidebar-width: 280px
```

**Typography:**
```css
--heading: "Space Mono", monospace
--body-font: "DM Sans", sans-serif
--mono: "Space Mono", monospace
```

**Effects:**
```css
--shadow-md: 0 8px 24px rgba(26, 31, 94, 0.18)
```

### CSS Organization

Files organized by module/component:

**Core Styles:**
- `src/core/styles/navbar.css`
- `src/core/styles/sidebar.css`
- `src/core/auth/styles/login.css`
- `src/core/auth/styles/forgotPasswordModal.css`
- `src/core/navigation/secondarySidebar/secondarySidebar.css`

**Module Styles:**
- `src/modules/admin/styles/adminDashboard.css`
- `src/modules/users/styles/userManagement.css`
- `src/modules/users/styles/userForms.css`
- `src/modules/users/styles/userDetails.css`
- `src/modules/users/styles/userTable.css`
- `src/modules/users/styles/users.css`
- `src/modules/landing/styles/landing.css`
- `src/modules/university/styles/universityStructure.css`
- `src/modules/university/styles/scopeModal.css`
- `src/modules/permissions/styles/permissions.css`
- `src/modules/permissions/styles/roles.css`

### Styling Features

1. **Responsive Design:**
   - Mobile breakpoint: 768px
   
