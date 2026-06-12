import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";

vi.mock("../core/api/apiClient", () => ({
  default: {
    get: vi.fn().mockResolvedValue({ data: [] }),
    post: vi.fn(),
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  },
}));
vi.mock("react-i18next", () => ({
  useTranslation: () => ({
    t: (k, opts) => opts?.defaultValue ?? k,
    i18n: { language: "en", on: vi.fn(), off: vi.fn() },
  }),
}));

import { DomainProvider, useDomain } from "../core/contexts/DomainContext";
import { AcademicProvider } from "../core/contexts/AcademicContext";
import { useScopeKeyPart } from "../core/query/scopedKeys";

function Probe() {
  const part = useScopeKeyPart();
  const { selectScopeNode, clearScope } = useDomain();
  return (
    <div>
      <span data-testid="scope">{String(part.scope)}</span>
      <button onClick={() => selectScopeNode({ id: "eng-1", name: "Engineering", type: "faculty" })}>
        set-scope
      </button>
      <button onClick={clearScope}>clear-scope</button>
    </div>
  );
}

const renderProbe = () => render(
  <DomainProvider>
    <AcademicProvider>
      <Probe />
    </AcademicProvider>
  </DomainProvider>
);

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});

describe("useScopeKeyPart (Scenario B: scope-reactive query keys)", () => {
  it("starts with a null scope when nothing is selected", () => {
    renderProbe();
    expect(screen.getByTestId("scope").textContent).toBe("null");
  });

  it("changes the key part when the navbar scope changes, and back on clear", () => {
    renderProbe();
    fireEvent.click(screen.getByText("set-scope"));
    expect(screen.getByTestId("scope").textContent).toBe("eng-1");
    fireEvent.click(screen.getByText("clear-scope"));
    expect(screen.getByTestId("scope").textContent).toBe("null");
  });

  it("persists the selected scope for the current session (isolated)", () => {
    renderProbe();
    fireEvent.click(screen.getByText("set-scope"));
    expect(JSON.parse(sessionStorage.getItem("capu_selected_scope_node")).id).toBe("eng-1");
    expect(localStorage.getItem("capu_selected_scope_node")).toBeNull();
  });
});
