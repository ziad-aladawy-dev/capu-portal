# Capital University Portal — Frontend (`capu-portal`)

The web client for the Capital University Student Portal: a dual-portal
(Admin/Staff + Student) single-page application built as a module-federated
shell. It talks to the .NET modular-monolith backend (`../src`).

> Arabic-first, bilingual (AR/EN) with full RTL support.

---

## 🧰 Tech Stack

| Concern | Choice |
|---|---|
| UI library | **React 19** (hooks, React Compiler) |
| Language | **JavaScript (JSX)** — TS migration planned (see `docs/MASTER_SPECIFICATION.md`) |
| Build | **Vite 8** (`esnext`), `@originjs/vite-plugin-federation` (shell) |
| Routing | **react-router-dom v7**, manifest-driven (`core/router/routeRegistry.js`) |
| Server state | **TanStack Query v5** |
| Client state | **Zustand v5** (`useScopeStore`, `useAcademicStore`) + React Contexts |
| HTTP | **Axios** with JWT refresh-queue + scope-header interceptor |
| Forms | **react-hook-form v7** + **zod** |
| i18n | **i18next** + **react-i18next** (12 JSON namespaces per language) |
| UI primitives | **Radix UI** + **lucide-react** icons |
| Drag & drop | **@dnd-kit** |
| Styling | Vanilla CSS + custom properties (design tokens) — **not Tailwind** |
| Testing | **Vitest** + **@testing-library/react** + jsdom |

---

## 🚀 Getting Started

### Prerequisites
- Node.js 18+
- The backend API running (default `http://localhost:5256`) — see `../README.md`.

### Install & run
```bash
cd frontend
npm install
npm run dev          # Vite dev server (HMR)
```

### Scripts
| Script | Purpose |
|---|---|
| `npm run dev` | Start the dev server |
| `npm run build` | Production build to `dist/` |
| `npm run preview` | Serve the production build locally |
| `npm run lint` | ESLint |
| `npm run test` | Run the Vitest suite once |
| `npm run test:watch` | Vitest in watch mode |

---

## ⚙️ Environment Setup

Vite loads `.env.*` files; only `VITE_`-prefixed vars reach the client.
Copy the template and adjust:

```bash
cp .env.example .env.development.local   # machine-specific overrides (gitignored)
```

| Variable | Description |
|---|---|
| `VITE_API_BASE_URL` | Backend REST base (include `/api`). **Only var consumed in code today.** |
| `VITE_WS_URL` | Real-time channel (notifications) — Phase 8 |
| `VITE_SENTRY_DSN` | Error monitoring — Phase 14 (empty = off) |
| `VITE_APP_VERSION` | Injected from `package.json` |
| `VITE_DEPLOY_ENV` | `development` \| `staging` \| `production` |
| `VITE_ENABLE_MOCKS` | Toggle mock data layer |
| `VITE_DEFAULT_LNG` | Default UI language (`ar`) |

> **Payments note:** the frontend calls the backend's `/api/payments/*`
> endpoints; the backend integrates with the HU Treasury System
> (Mastercard / Bank Misr / eFinance). There is no separate frontend
> payment-gateway host. See `docs/payment/`.

---

## 🗂️ Project Structure

```
src/
├── core/                 # Cross-cutting infrastructure
│   ├── api/              # Axios client + interceptors
│   ├── auth/             # AuthContext, PermissionContext, guards, login pages
│   ├── components/       # Shared UI (DataTable, Skeleton, Toast, ErrorBoundary…)
│   ├── contexts/         # Domain / Academic / StickySelection providers
│   ├── i18n/             # i18next config + locales/{en,ar}/*.json (12 namespaces)
│   ├── layouts/          # Dashboard / login layouts
│   ├── manifests/        # Route + navigation manifests
│   ├── query/            # TanStack Query client
│   ├── router/           # AppRouter + manifest-driven routeRegistry
│   ├── stores/           # Zustand stores (scope, academic)
│   └── styles/           # tokens.css (design tokens), print.css, navbar/sidebar
└── modules/              # Feature modules, each exporting routes.js
    ├── studentPortal/    admin/  studentServices/  courses/  schedule/
    ├── invoices/  transactions/  notifications/  permissions/  …
```

Each module is lazy-loaded via `React.lazy` and registered through its
`routes.js` export, aggregated in `core/router/routeRegistry.js`.

### Key cross-cutting systems
- **Permissions:** `module.resource.action` with scope-aware resolution;
  enforced at route level (`RouteGuard`) and UI level (`PermissionGate`).
- **Scope:** structural (university node) + temporal (academic year/semester)
  scopes auto-attached to requests via headers/query params.

---

## 🌍 Internationalization & RTL

- Languages: **Arabic (default/fallback)** and **English**.
- Translations live in `src/core/i18n/locales/{en,ar}/` as 12 JSON
  namespaces: `common, auth, navigation, dashboard, landing, students,
  staff, structure, studentServices, notifications, permissions, validation`.
- Direction switches automatically on language change
  (`document.documentElement.dir`, `body.rtl`).

### Translation workflow
1. Add the key to **both** `en/<namespace>.json` and `ar/<namespace>.json`.
2. Use `category.subcategory.action` key naming
   (e.g. `course.catalog.search`, not `search_courses`).
3. Access via `const { t } = useTranslation(); t('course.catalog.search')`.
4. Interpolate with `t('key', { count })`; pluralize where relevant.
5. Never hardcode user-facing strings.

---

## 🧪 Testing

- **Runner:** Vitest (`jsdom` environment, setup in `src/test/setup.js`).
- **Component tests:** `@testing-library/react` + `@testing-library/jest-dom`.
- Run `npm run test` (CI) or `npm run test:watch` (local).

Target coverage and the full strategy (unit/integration/E2E, MSW mocking,
a11y) are described in `docs/MASTER_SPECIFICATION.md` (Phase 12).

---

## 🚢 Deployment

- Multi-stage Docker build → static assets served by nginx (see
  `../deployment/` and `../docker-compose.yaml`).
- Production build: `npm run build` (outputs `dist/`).
- Environment values are injected at build time from `.env.production`
  (real secrets via the CI/CD environment or `.env.production.local`).

---

## 🤝 Contributing

- Match the surrounding code style (vanilla CSS + tokens, JSX, existing
  naming). No Tailwind, no TypeScript yet.
- Forms use `react-hook-form` + `zod`; data fetching uses TanStack Query
  (avoid raw `useEffect` + `useState` fetching in new code).
- All new user-facing text must be i18n'd in both languages.
- Run `npm run lint` and `npm run test` before opening a PR.
- The authoritative roadmap is `docs/MASTER_SPECIFICATION.md`.
