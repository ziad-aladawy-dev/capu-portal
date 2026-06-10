import { createContext, useContext, useState, useCallback, useEffect } from "react";

const StickySelectionContext = createContext(null);

const STORAGE_KEY = "capu_pinned_user";

function readStoredSelection() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed && parsed.id ? parsed : null;
  } catch {
    return null;
  }
}

export function StickySelectionProvider({ children }) {
  const [selected, setSelected] = useState(readStoredSelection);

  // Login/logout clears the stored pin — resync so the previous user's
  // selection never carries over into a new session.
  useEffect(() => {
    const resync = () => setSelected(readStoredSelection());
    window.addEventListener("capu:auth-changed", resync);
    return () => window.removeEventListener("capu:auth-changed", resync);
  }, []);

  const select = useCallback((entity) => {
    setSelected(entity);
    try {
      if (entity && entity.id) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({
          id: entity.id,
          name: entity.name,
          code: entity.code,
          type: entity.type,
        }));
      }
    } catch { /* storage unavailable — pin stays in-memory */ }
  }, []);

  const clear = useCallback(() => {
    setSelected(null);
    try { localStorage.removeItem(STORAGE_KEY); } catch { /* ignore */ }
  }, []);

  const value = {
    selected,
    select,
    clear,
    isActive: selected !== null,
  };

  return (
    <StickySelectionContext.Provider value={value}>
      {children}
    </StickySelectionContext.Provider>
  );
}

export function useStickySelection() {
  const context = useContext(StickySelectionContext);

  if (!context) {
    throw new Error("useStickySelection must be used within a StickySelectionProvider");
  }

  return context;
}
