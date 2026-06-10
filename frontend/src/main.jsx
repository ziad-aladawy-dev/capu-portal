import React from "react";
import ReactDOM from "react-dom/client";

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
import useAcademicStore from "./core/stores/useAcademicStore";
import useScopeStore from "./core/stores/useScopeStore";
import "./core/i18n/i18n";

// Hydrate Zustand persisted stores on app boot
useScopeStore.getState().hydrate();
useAcademicStore.getState().hydrate();

// Load academic data on boot if authenticated
const token = localStorage.getItem("accessToken");
if (token) {
  useAcademicStore.getState().loadAcademicYears();
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
