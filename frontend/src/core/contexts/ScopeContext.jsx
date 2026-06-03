import { createContext, useContext, useState, useEffect } from "react";
import { useTranslation } from "react-i18next";

const getLocalizedText = (text, lang) => {
  if (!text) return "";
  try {
    const parsed = JSON.parse(text);
    return parsed[lang] || parsed.ar || parsed.en || text;
  } catch {
    return text;
  }
};

const ScopeContext = createContext();

export const ScopeProvider = ({ children }) => {
  const { i18n } = useTranslation();
  const [selectedScope, setSelectedScope] = useState(null);

  useEffect(() => {
    const saved = localStorage.getItem("globalScope");
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        setSelectedScope(parsed);
      } catch (e) {}
    }
  }, []);

  useEffect(() => {
    if (selectedScope && selectedScope.name) {
      const originalName = selectedScope.originalName || selectedScope.name;
      const newLocalizedName = getLocalizedText(originalName, i18n.language);
      if (newLocalizedName !== selectedScope.localizedName) {
        const updatedScope = {
          ...selectedScope,
          localizedName: newLocalizedName,
        };
        setSelectedScope(updatedScope);
        localStorage.setItem("globalScope", JSON.stringify(updatedScope));
      }
    }
  }, [i18n.language, selectedScope]);

  const updateScope = (scope) => {
    const scopeToStore = {
      id: scope.id,
      name: scope.name,
      originalName: scope.originalName || scope.name,
      localizedName: scope.localizedName || scope.name,
      type: scope.type,
      path: scope.path,
    };
    setSelectedScope(scopeToStore);
    if (scopeToStore) {
      localStorage.setItem("globalScope", JSON.stringify(scopeToStore));
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