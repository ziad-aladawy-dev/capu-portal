import React from "react";
import ReactDOM from "react-dom/client";

import { BrowserRouter } from "react-router-dom";

import App from "./App";

import "./index.css";

import { DomainProvider } from "./core/contexts/DomainContext";
import { AcademicProvider } from "./core/contexts/AcademicContext";
import { StickySelectionProvider } from "./core/contexts/StickySelectionContext";
import { AuthProvider } from "./core/auth/AuthContext";
import { PermissionProvider } from "./core/auth/PermissionContext";
import { ToastProvider } from "./core/components/Toast";
import ErrorBoundary from "./core/components/ErrorBoundary";

ReactDOM.createRoot(
  document.getElementById("root")
).render(
  <BrowserRouter>
    <ErrorBoundary>
      <AuthProvider>
        <PermissionProvider>
          <ToastProvider>
            <DomainProvider>
              <AcademicProvider>
                <StickySelectionProvider>
                  <App />
                </StickySelectionProvider>
              </AcademicProvider>
            </DomainProvider>
          </ToastProvider>
        </PermissionProvider>
      </AuthProvider>
    </ErrorBoundary>
  </BrowserRouter>
);
