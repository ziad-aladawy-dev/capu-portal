import { create } from "zustand";

const STORAGE_KEY = "capu_dashboard_layout";

// Canonical widget keys + default ordering. New widgets added here are appended
// for users who already have a persisted layout (see hydrate()).
export const DEFAULT_WIDGET_ORDER = [
  "profile",
  "stats",
  "actions",
  "schedule",
  "financial",
  "quicklinks",
  "services",
];

function persist(state) {
  try {
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ widgetOrder: state.widgetOrder, hiddenWidgets: state.hiddenWidgets })
    );
  } catch {}
}

const useDashboardLayoutStore = create((set, get) => ({
  widgetOrder: DEFAULT_WIDGET_ORDER,
  hiddenWidgets: [],
  customizing: false,

  hydrate: () => {
    try {
      const saved = JSON.parse(localStorage.getItem(STORAGE_KEY));
      if (saved && Array.isArray(saved.widgetOrder)) {
        // Keep only known widgets, then append any new ones not yet in the
        // saved order so a new release's widgets still appear.
        const known = saved.widgetOrder.filter((k) => DEFAULT_WIDGET_ORDER.includes(k));
        const appended = DEFAULT_WIDGET_ORDER.filter((k) => !known.includes(k));
        set({
          widgetOrder: [...known, ...appended],
          hiddenWidgets: Array.isArray(saved.hiddenWidgets)
            ? saved.hiddenWidgets.filter((k) => DEFAULT_WIDGET_ORDER.includes(k))
            : [],
        });
      }
    } catch {}
  },

  setCustomizing: (value) => set({ customizing: Boolean(value) }),

  setOrder: (widgetOrder) => {
    set({ widgetOrder });
    persist(get());
  },

  toggleHidden: (key) => {
    const hidden = get().hiddenWidgets;
    const next = hidden.includes(key)
      ? hidden.filter((k) => k !== key)
      : [...hidden, key];
    set({ hiddenWidgets: next });
    persist(get());
  },

  resetLayout: () => {
    set({ widgetOrder: DEFAULT_WIDGET_ORDER, hiddenWidgets: [] });
    persist(get());
  },
}));

export default useDashboardLayoutStore;
