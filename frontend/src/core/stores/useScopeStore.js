import { create } from "zustand";

const STORAGE_KEY = "capu_selected_scope_node";

const useScopeStore = create((set, get) => ({
  scopeNode: null,
  selectedDomain: null,
  ready: false,

  hydrate: () => {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (parsed && parsed.id) {
          set({
            scopeNode: parsed,
            selectedDomain: parsed,
          });
        }
      }
    } catch {}
    set({ ready: true });
  },

  selectScopeNode: (node) => {
    const value = node && node.id
      ? { id: node.id, name: node.name, type: node.type }
      : null;
    set({
      scopeNode: value,
      selectedDomain: value,
    });
    if (value) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  },

  clearScope: () => {
    set({ scopeNode: null, selectedDomain: null });
    localStorage.removeItem(STORAGE_KEY);
  },
}));

export default useScopeStore;
