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
      JSON.stringify({ widgetOrder: ["fees", "bogus", "schedule"], hiddenWidgets: ["grades", "ghost"] })
    );
    useDashboardLayoutStore.getState().hydrate();
    const s = useDashboardLayoutStore.getState();

    expect(s.widgetOrder[0]).toBe("fees");
    expect(s.widgetOrder[1]).toBe("schedule");
    expect(s.widgetOrder).not.toContain("bogus");
    // every default widget is still present (appended)
    expect([...s.widgetOrder].sort()).toEqual([...DEFAULT_WIDGET_ORDER].sort());
    // unknown hidden key dropped
    expect(s.hiddenWidgets).toEqual(["grades"]);
  });

  it("toggleHidden persists to localStorage", () => {
    useDashboardLayoutStore.getState().toggleHidden("fees");
    expect(useDashboardLayoutStore.getState().hiddenWidgets).toContain("fees");
    expect(JSON.parse(localStorage.getItem(KEY)).hiddenWidgets).toContain("fees");

    useDashboardLayoutStore.getState().toggleHidden("fees"); // toggle back off
    expect(useDashboardLayoutStore.getState().hiddenWidgets).not.toContain("fees");
  });

  it("resetLayout restores defaults", () => {
    useDashboardLayoutStore.getState().toggleHidden("schedule");
    useDashboardLayoutStore.getState().setOrder(["fees", "schedule"]);
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
