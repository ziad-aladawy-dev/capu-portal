```
frontend/
├── src/
│   ├── core/
│   │   ├── api/                  // Axios instance and interceptors
│   │   ├── auth/                 
│   │   │   └── pages/
│   │   │       └── Login.jsx     // Login page component
│   │   ├── pages/                
│   │   │   └── LandingPage.jsx   // Public landing page
│   │   ├── layouts/              // App wrappers, navigation, sidebars
│   │   ├── components/           // Shared UI components (Buttons, Modals, etc.)
│   │   ├── stores/               // Global states (Zustand, Context, Redux)
│   │   ├── i18n/                 // Localization files
│   │   └── router/
│   │       └── index.jsx         // Root router configuration
│   │
│   ├── modules/
│   │   ├── student/
│   │   │   ├── pages/
│   │   │   ├── components/
│   │   │   ├── api/
│   │   │   ├── stores/
│   │   │   ├── routes.jsx        // Module-specific route definitions
│   │   │   └── index.js          // Module configuration and exports
│   │   ├── enrollment/
│   │   └── complaints/
│   │
│   ├── modules.config.js         // ⭐ Registry to plug all modules into the app
│   ├── App.jsx
│   └── main.jsx
├── package.json
└── vite.config.js
```
