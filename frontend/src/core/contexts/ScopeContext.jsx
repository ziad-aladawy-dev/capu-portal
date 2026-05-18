import { createContext, useContext, useState, useEffect } from "react";

const ScopeContext = createContext();

export const ScopeProvider = ({ children }) => {
  const [selectedScope, setSelectedScope] = useState(null);

  useEffect(() => {
    const saved = localStorage.getItem("globalScope");
    if (saved) {
      try {
        setSelectedScope(JSON.parse(saved));
      } catch (e) {}
    }
  }, []);

  const updateScope = (scope) => {
    setSelectedScope(scope);
    if (scope) {
      localStorage.setItem("globalScope", JSON.stringify(scope));
    } else {
      localStorage.removeItem("globalScope");
    }
  };

  return (
    <ScopeContext.Provider value={{ selectedScope, updateScope }}>
      {children}
    </ScopeContext.Provider>
  );
};

export const useScope = () => useContext(ScopeContext);