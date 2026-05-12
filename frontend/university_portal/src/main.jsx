import React from "react";
import ReactDOM from "react-dom/client";

import { BrowserRouter } from "react-router-dom";

import App from "./App";

import "./index.css";

import { DomainProvider } from "./core/contexts/DomainContext";

import { AcademicProvider } from "./core/contexts/AcademicContext";

ReactDOM.createRoot(
  document.getElementById("root")
).render(
  <BrowserRouter>
    <DomainProvider>
      <AcademicProvider>
        <App />
      </AcademicProvider>
    </DomainProvider>
  </BrowserRouter>
);