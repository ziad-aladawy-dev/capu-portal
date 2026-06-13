import { createContext, useContext, useState, useEffect, useCallback, useRef } from "react";
import * as structureService from "../services/structureService";

const DomainContext = createContext();

const STORAGE_KEY = "capu_selected_scope_node";

function persistScopeNode(node) {
  if (node && node.id) {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ id: node.id, name: node.name, type: node.type, order: node.order }));
  } else {
    sessionStorage.removeItem(STORAGE_KEY);
  }
}

export const DomainProvider = ({ children }) => {
  const [scopeNode, setScopeNode] = useState(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const saved = sessionStorage.getItem(STORAGE_KEY);
        if (saved) {
          const parsed = JSON.parse(saved);
          if (parsed && parsed.id) {
            if (!cancelled) setScopeNode(parsed);
            if (!cancelled) setReady(true);
            return;
          }
        }
        // No saved scope — auto-fetch university root on login
        const token = localStorage.getItem("accessToken");
        if (!token) {
          if (!cancelled) setReady(true);
          return;
        }
        const tree = await structureService.fetchStructureTree();
        if (cancelled) return;
        const roots = Array.isArray(tree) ? tree : [];
        if (roots.length > 0) {
          const root = { id: roots[0].id, name: roots[0].name, type: roots[0].type, order: roots[0].order };
          if (!cancelled) {
            setScopeNode(root);
            persistScopeNode(root);
          }
        }
      } catch {}
      if (!cancelled) setReady(true);
    })();
    return () => { cancelled = true; };
  }, []);

  const selectScopeNode = useCallback((node) => {
    const value = node && node.id ? { id: node.id, name: node.name, type: node.type, order: node.order } : null;
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
