import { createContext, useContext, useState, useCallback, useEffect } from "react";
import { useAuth } from "../auth/useAuth";
import * as structureService from "../services/structureService";

const DomainContext = createContext(null);

export function DomainProvider({ children }) {
  const { activeScope, authorizedScopes, isAuthenticated } = useAuth();

  const [selectedDomain, setSelectedDomain] = useState({
    id: "root-001",
    name: "Capital University",
    type: "University",
    path: "/root-001",
  });
  const [domains, setDomains] = useState([]);
  const [domainsLoading, setDomainsLoading] = useState(false);

  useEffect(() => {
    if (!isAuthenticated) return;
    setDomainsLoading(true);
    structureService.fetchFaculties()
      .then((data) => {
        setDomains(Array.isArray(data) ? data : []);
      })
      .catch(() => {
        setDomains([]);
      })
      .finally(() => setDomainsLoading(false));
  }, [isAuthenticated]);

  useEffect(() => {
    if (!activeScope?.structural?.nodeId || !domains.length) return;
    const match = domains.find((d) => d.id === activeScope.structural.nodeId);
    if (match) {
      setSelectedDomain(match);
    }
  }, [activeScope, domains]);

  const selectDomain = useCallback((domain) => {
    setSelectedDomain(domain);
  }, []);

  const value = {
    selectedDomain,
    selectDomain,
    domains,
    domainsLoading,
  };

  return (
    <DomainContext.Provider value={value}>
      {children}
    </DomainContext.Provider>
  );
}

export function useDomain() {
  const context = useContext(DomainContext);
  if (!context) {
    throw new Error("useDomain must be used within a DomainProvider");
  }
  return context;
}
