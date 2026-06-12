import ReactDOM from "react-dom/client";
import * as Sentry from "@sentry/react";

import { BrowserRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";

import App from "./App";

import "./core/styles/tokens.css";
import "./index.css";
import "./core/styles/print.css";

import { DomainProvider } from "./core/contexts/DomainContext";
import { AcademicProvider } from "./core/contexts/AcademicContext";
import { StickySelectionProvider } from "./core/contexts/StickySelectionContext";
import { AuthProvider } from "./core/auth/AuthContext";
import { PermissionProvider } from "./core/auth/PermissionContext";
import { ToastProvider } from "./core/components/Toast";
import ErrorBoundary from "./core/components/ErrorBoundary";
import { queryClient } from "./core/query/queryClient";
import "./core/i18n/i18n";

const SENTRY_DSN = import.meta.env.VITE_SENTRY_DSN;
const DEPLOY_ENV = import.meta.env.VITE_DEPLOY_ENV || "development";

if (SENTRY_DSN) {
  Sentry.init({
    dsn: SENTRY_DSN,
    environment: DEPLOY_ENV,
    integrations: [
      Sentry.browserTracingIntegration(),
      Sentry.replayIntegration(),
    ],
    // Performance Monitoring
    tracesSampleRate: 1.0, // Capture 100% of the transactions in dev/staging
    // Session Replay
    replaysSessionSampleRate: 0.1, // This sets the sample rate at 10%. You may want to change it to 100% while in development and then sample at a lower rate in production.
    replaysOnErrorSampleRate: 1.0, // If you're not already sampling the entire session, change the sample rate to 100% when sampling sessions where errors occur.
  });
}

ReactDOM.createRoot(
  document.getElementById("root")
).render(
  <BrowserRouter>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <ToastProvider>
            <DomainProvider>
              <AcademicProvider>
                <PermissionProvider>
                  <StickySelectionProvider>
                    <App />
                  </StickySelectionProvider>
                </PermissionProvider>
              </AcademicProvider>
            </DomainProvider>
          </ToastProvider>
        </AuthProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  </BrowserRouter>
);
