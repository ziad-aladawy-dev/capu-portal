import { describe, it, expect, beforeEach } from "vitest";
import useDashboardLayoutStore, {
  DEFAULT_WIDGET_ORDER,
} from "../core/stores/useDashboardLayoutStore";

const KEY = "capu_dashboard_layout";

beforeEach(() => {
  localStorage.clear();
  useDashboardLayoutStore.setState({
    widgetOrder: DEFAULT_WIDGET_ORDER,
    hiddenWidgets: [],
    customizing: false,
  });
});

describe("useDashboardLayoutStore", () => {
  it("hydrate keeps saved order, drops unknown widgets, appends new ones", () => {
    localStorage.setItem(
      KEY,
      JSON.stringify({ widgetOrder: ["services", "bogus", "profile"], hiddenWidgets: ["stats", "ghost"] })
    );
    useDashboardLayoutStore.getState().hydrate();
    const s = useDashboardLayoutStore.getState();

    expect(s.widgetOrder[0]).toBe("services");
    expect(s.widgetOrder[1]).toBe("profile");
    expect(s.widgetOrder).not.toContain("bogus");
    // every default widget is still present (appended)
    expect([...s.widgetOrder].sort()).toEqual([...DEFAULT_WIDGET_ORDER].sort());
    // unknown hidden key dropped
    expect(s.hiddenWidgets).toEqual(["stats"]);
  });

  it("toggleHidden persists to localStorage", () => {
    useDashboardLayoutStore.getState().toggleHidden("services");
    expect(useDashboardLayoutStore.getState().hiddenWidgets).toContain("services");
    expect(JSON.parse(localStorage.getItem(KEY)).hiddenWidgets).toContain("services");

    useDashboardLayoutStore.getState().toggleHidden("services"); // toggle back off
    expect(useDashboardLayoutStore.getState().hiddenWidgets).not.toContain("services");
  });

  it("resetLayout restores defaults", () => {
    useDashboardLayoutStore.getState().toggleHidden("profile");
    useDashboardLayoutStore.getState().setOrder(["services", "profile"]);
    useDashboardLayoutStore.getState().resetLayout();
    const s = useDashboardLayoutStore.getState();
    expect(s.hiddenWidgets).toEqual([]);
    expect(s.widgetOrder).toEqual(DEFAULT_WIDGET_ORDER);
  });

  it("hydrate is a no-op when nothing is saved", () => {
    useDashboardLayoutStore.getState().hydrate();
    expect(useDashboardLayoutStore.getState().widgetOrder).toEqual(DEFAULT_WIDGET_ORDER);
  });
});
