import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import "./index.css";
import { ScopeProvider } from "./core/contexts/ScopeContext";
import { AcademicProvider } from "./core/contexts/AcademicContext";
import "./core/i18n/i18n";

ReactDOM.createRoot(
  document.getElementById("root")
).render(
  <BrowserRouter>
    <ScopeProvider>
      <AcademicProvider>
        <App />
      </AcademicProvider>
    </ScopeProvider>
  </BrowserRouter>
);