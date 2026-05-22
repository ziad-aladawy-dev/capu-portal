import PropTypes from "prop-types";
import { createContext, useContext, useState, useCallback } from "react";

const StickySelectionContext = createContext(null);

export function StickySelectionProvider({ children }) {
  const [selected, setSelected] = useState(null);

  const select = useCallback((entity) => {
    setSelected(entity);
  }, []);

  const clear = useCallback(() => {
    setSelected(null);
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

StickySelectionProvider.propTypes = {
  children: PropTypes.node,
};
