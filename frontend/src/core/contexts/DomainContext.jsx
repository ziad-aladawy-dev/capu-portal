import { createContext, useContext, useState, useEffect, useCallback } from "react";

const DomainContext = createContext();

const STORAGE_KEY = "capu_selected_scope_node";

export const DomainProvider = ({ children }) => {
  const [scopeNode, setScopeNode] = useState(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    try {
      const saved = sessionStorage.getItem(STORAGE_KEY);
      if (saved) {
        const parsed = JSON.parse(saved);
        if (parsed && parsed.id) setScopeNode(parsed);
      }
    } catch {}
    setReady(true);
  }, []);

  const selectScopeNode = useCallback((node) => {
    const value = node && node.id ? { id: node.id, name: node.name, type: node.type } : null;
    setScopeNode(value);
    if (value) {
      sessionStorage.setItem(STORAGE_KEY, JSON.stringify(value));
    } else {
      sessionStorage.removeItem(STORAGE_KEY);
    }
  }, []);

  const clearScope = useCallback(() => {
    setScopeNode(null);
    sessionStorage.removeItem(STORAGE_KEY);
  }, []);

  return (
    <DomainContext.Provider
      value={{
        selectedDomain: scopeNode,
        selectDomain: selectScopeNode,
        scopeNode,
        selectScopeNode,
        clearScope,
        domains: scopeNode ? [scopeNode] : [],
        domainsLoading: false,
        ready,
      }}
    >
      {children}
    </DomainContext.Provider>
  );
};

export const useDomain = () => useContext(DomainContext);
