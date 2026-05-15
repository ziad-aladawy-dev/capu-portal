import React from "react";
import ReactDOM from "react-dom/client";

import { BrowserRouter } from "react-router-dom";

import App from "./App";

import "./index.css";

import { DomainProvider } from "./core/contexts/DomainContext";
import { AcademicProvider } from "./core/contexts/AcademicContext";
import { AuthProvider } from "./core/auth/AuthContext";
import { PermissionProvider } from "./core/auth/PermissionContext";

ReactDOM.createRoot(
  document.getElementById("root")
).render(
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
);
