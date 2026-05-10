import { useContext } from "react";
import { ScopeContext } from "../contexts/ScopeContext";

export const useScope = () => {
  const context = useContext(ScopeContext);
  if (!context) {
    throw new Error("useScope must be used within ScopeProvider");
  }
  return context;
};
