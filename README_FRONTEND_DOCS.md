# CAPU Portal Frontend - Documentation Index

**Last Updated:** May 19, 2026

This directory contains comprehensive documentation about the CAPU Portal frontend architecture and structure.

## Available Documentation

### 1. **FRONTEND_ARCHITECTURE_REPORT.md** (Recommended for detailed study)
   - **Size:** 318 lines, 8.3KB
   - **Purpose:** Comprehensive technical analysis
   - **Best For:** Understanding architecture patterns, detailed implementation specifics
   - **Contents:**
     - Project overview & tech stack
     - Complete routing setup & route guards
     - Component architecture with directory tree
     - Styling methodology & CSS organization
     - API client configuration & services
     - State management with all contexts
     - Complete pages & modules documentation
     - UI libraries & design system
     - Full directory structure tree
     - Summary & architecture patterns

### 2. **FRONTEND_QUICK_REFERENCE.md** (Recommended for quick lookup)
   - **Size:** 331 lines, 8.1KB
   - **Purpose:** Quick reference guide for developers
   - **Best For:** Looking up specific patterns, quick reminders, development workflow
   - **Contents:**
     - Routing setup quick ref
     - Component architecture overview
     - Styling guide
     - API client quick setup
     - State management patterns
     - Modules overview table
     - Auth & authorization reference
     - UI libraries checklist
     - Manifest system guide
     - Key files reference table
     - Development workflow instructions
     - Environment configuration
     - Key metrics summary

---

## Quick Navigation

### For Understanding the Architecture
1. Start with: **FRONTEND_ARCHITECTURE_REPORT.md** → "Project Overview" section
2. Then read: "Routing Setup" → "Component Architecture"
3. Deep dive: "State Management" → "Pages & Modules"

### For Development
1. Check: **FRONTEND_QUICK_REFERENCE.md** → "Development Workflow"
2. Reference: "Key Files Reference" table
3. Look up: Specific topic (routing, state management, etc.)

### For Onboarding New Developers
1. Share: **FRONTEND_QUICK_REFERENCE.md** first
2. Then provide: **FRONTEND_ARCHITECTURE_REPORT.md** for deep understanding
3. Direct to: Specific module documentation

---

## Key Facts at a Glance

**Technology Stack:**
- React 19.2.6 + React Router DOM 7.15.0
- Vite 8.0.12 (build tool)
- Axios 1.16.1 (HTTP client)
- Lucide React 1.14.0 (icons)

**Architecture Pattern:**
- Modular design with 7 independent feature modules
- Context-based state management (no Redux)
- Manifest-driven routing system
- Custom CSS with CSS variables (no Tailwind)

**Key Features:**
- Resource-based permission system (5 levels)
- Permission-guarded routes & menus
- Dynamic component loading from manifests
- Responsive design (768px mobile, 1024px tablet)
- Dark mode support

**Project Structure:**
- 100+ JSX/JS files
- 18 CSS files
- 7 manifest.json files
- 20+ core components
- 40+ module components
- 7 custom hooks (+ more module-specific)

**State Management:**
- 5 global contexts (Auth, Permission, Domain, Academic, Scope)
- useReducer for complex state
- localStorage for persistence

**API Configuration:**
- Base URL: `http://localhost:5256` (configurable)
- Automatic Bearer token injection
- 401 auto-logout handling
- Request/Response interceptors

---

## Directory Structure Overview

```
capu-portal/
├── frontend/
│   ├── src/
│   │   ├── core/                    # Core infrastructure
│   │   │   ├── api/                 # HTTP client
│   │   │   ├── auth/                # Auth & permissions
│   │   │   ├── contexts/            # Global state
│   │   │   ├── layouts/             # Layout components
│   │   │   ├── navigation/          # Navbar & Sidebar
│   │   │   ├── router/              # Routing config
│   │   │   ├── services/            # API services
│   │   │   ├── manifests/           # Manifest loader
│   │   │   └── styles/              # Core CSS
│   │   ├── modules/                 # 7 feature modules
│   │   │   ├── admin/
│   │   │   ├── users/
│   │   │   ├── university/
│   │   │   ├── landing/
│   │   │   ├── permissions/
│   │   │   ├── staff/
│   │   │   └── students/
│   │   ├── App.jsx
│   │   ├── main.jsx
│   │   └── index.css                # Global CSS + variables
│   ├── package.json
│   ├── vite.config.js
│   └── index.html
├── FRONTEND_ARCHITECTURE_REPORT.md  ← Detailed guide
├── FRONTEND_QUICK_REFERENCE.md      ← Quick lookup
└── README_FRONTEND_DOCS.md          ← This file
```

---

## Common Questions

### Q: How do I add a new module?
**A:** See FRONTEND_QUICK_REFERENCE.md → "Development Workflow" → "Adding a New Module"

### Q: How are routes protected?
**A:** See FRONTEND_ARCHITECTURE_REPORT.md → "Routing Setup" or QUICK_REFERENCE → "Routing Setup"

### Q: Where is the permission system?
**A:** See FRONTEND_ARCHITECTURE_REPORT.md → "State Management" → "PermissionContext"

### Q: How does the API client work?
**A:** See FRONTEND_ARCHITECTURE_REPORT.md → "API Client Configuration"

### Q: What contexts are available?
**A:** See FRONTEND_QUICK_REFERENCE.md → "State Management Patterns" table

### Q: Which UI library is used?
**A:** Custom CSS (no Material-UI, Chakra, etc.). Icons from Lucide React.

### Q: How is styling organized?
**A:** 18 CSS files organized by component/module. See both docs → "Styling Methodology"

### Q: How do I check permissions in a component?
**A:** Use `usePermission().can(resource, minLevel)`. Example in both docs.

### Q: Where are the environment variables?
**A:** VITE_API_BASE_URL in .env. See QUICK_REFERENCE → "Environment Configuration"

---

## File Locations

### Core Entry Points
- **React Entry:** `frontend/src/main.jsx`
- **App Entry:** `frontend/src/App.jsx`
- **Router Entry:** `frontend/src/core/router/AppRouter.jsx`

### Key Configuration Files
- **Package Config:** `frontend/package.json`
- **Build Config:** `frontend/vite.config.js`
- **HTML Entry:** `frontend/index.html`
- **Global CSS:** `frontend/src/index.css`

### Module Manifests
- `frontend/src/modules/*/manifest.json` (7 files)
- Loaded by: `frontend/src/core/manifests/manifestLoader.js`

### API Configuration
- **Client:** `frontend/src/core/api/apiClient.js`
- **Services:** `frontend/src/core/services/*.js` and `frontend/src/modules/*/services/*.js`

### Authentication
- **Auth Context:** `frontend/src/core/auth/AuthContext.jsx`
- **Permission Context:** `frontend/src/core/auth/PermissionContext.jsx`
- **Auth Hooks:** `frontend/src/core/auth/useAuth.js` & `usePermission.js`
- **Route Guard:** `frontend/src/core/auth/RouteGuard.jsx`

---

## Related Documentation

This documentation covers the **frontend/src directory structure** as requested. For other aspects:
- Backend architecture: See backend documentation
- Deployment: See deployment guides
- Testing: See testing documentation

---

## How to Use These Documents

### Scenario 1: Understanding the whole system
1. Read this file first (orientation)
2. Read FRONTEND_ARCHITECTURE_REPORT.md (comprehensive overview)
3. Refer to FRONTEND_QUICK_REFERENCE.md (as needed)

### Scenario 2: Quick lookup while coding
1. Use FRONTEND_QUICK_REFERENCE.md search (Ctrl+F)
2. Find relevant section
3. Follow code examples

### Scenario 3: Adding new features
1. Check "Development Workflow" in QUICK_REFERENCE.md
2. Find related module in ARCHITECTURE_REPORT.md
3. Reference "Key Files Reference" for relevant files

### Scenario 4: Onboarding team members
1. Print/share both Markdown files
2. Have them read QUICK_REFERENCE.md first
3. Point them to specific sections as questions arise

---

## Key Takeaways

✓ **Modular** - 7 independent feature modules
✓ **Scalable** - Manifest-driven architecture
✓ **Secure** - Resource-based permission system
✓ **Clean** - Organized core/modules separation
✓ **Maintainable** - Custom CSS, no heavy dependencies
✓ **Modern** - Latest React, Vite, ES2024 support
✓ **Responsive** - Mobile-first design
✓ **Accessible** - Context-based state, meaningful hierarchy

---

## Document Statistics

| Document | Lines | Size | Topics | Tables |
|----------|---
