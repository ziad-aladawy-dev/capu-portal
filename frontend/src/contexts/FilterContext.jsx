import React, { createContext, useState, useCallback } from "react";
import { FILTER_CATEGORIES } from "../lib/constants";

export const FilterContext = createContext();

const DEFAULT_FILTER_STATE = {
  collegeId: null,
  programTypeId: null,
  programId: null,
  academicYearId: null,
  semesterId: null,
  entityId: null
};

export const FilterProvider = ({ children }) => {
  const [currentCategory, setCurrentCategory] = useState(FILTER_CATEGORIES.STUDENTS);
  
  // Store filters per category
  const [filters, setFilters] = useState({
    [FILTER_CATEGORIES.STUDENTS]: { ...DEFAULT_FILTER_STATE },
    [FILTER_CATEGORIES.ADMIN]: { ...DEFAULT_FILTER_STATE },
    [FILTER_CATEGORIES.FINANCIAL]: { ...DEFAULT_FILTER_STATE },
    [FILTER_CATEGORIES.REGISTRATION]: { ...DEFAULT_FILTER_STATE }
  });

  const setCurrentCategoryWithReset = useCallback((category) => {
    setCurrentCategory(category);
    // Note: filters persist per category (sticky), don't reset here
  }, []);

  const setFilter = useCallback((category, key, value) => {
    setFilters(prev => ({
      ...prev,
      [category]: {
        ...prev[category],
        [key]: value
      }
    }));
  }, []);

  const updateFilter = useCallback((key, value) => {
    setFilter(currentCategory, key, value);
  }, [currentCategory, setFilter]);

  const clearCategoryFilters = useCallback((category) => {
    setFilters(prev => ({
      ...prev,
      [category]: { ...DEFAULT_FILTER_STATE }
    }));
  }, []);

  const clearCurrentCategoryFilters = useCallback(() => {
    clearCategoryFilters(currentCategory);
  }, [currentCategory, clearCategoryFilters]);

  const clearAllFilters = useCallback(() => {
    setFilters({
      [FILTER_CATEGORIES.STUDENTS]: { ...DEFAULT_FILTER_STATE },
      [FILTER_CATEGORIES.ADMIN]: { ...DEFAULT_FILTER_STATE },
      [FILTER_CATEGORIES.FINANCIAL]: { ...DEFAULT_FILTER_STATE },
      [FILTER_CATEGORIES.REGISTRATION]: { ...DEFAULT_FILTER_STATE }
    });
  }, []);

  /**
   * Cascade clearing logic
   * When a parent filter changes, clear all dependent filters
   */
  const handleCascadeClear = useCallback((key, value) => {
    setFilters(prev => {
      const newFilters = { ...prev };
      
      // Cascade clearing for structural hierarchy
      if (key === "collegeId") {
        // Changing college clears: programType, program, entity
        newFilters[currentCategory] = {
          ...newFilters[currentCategory],
          [key]: value,
          programTypeId: null,
          programId: null,
          entityId: null
        };
      } else if (key === "programId") {
        // Changing program clears: entity only
        newFilters[currentCategory] = {
          ...newFilters[currentCategory],
          [key]: value,
          entityId: null
        };
      } else if (key === "academicYearId" || key === "semesterId") {
        // Temporal filters don't cascade (separate from structural)
        newFilters[currentCategory] = {
          ...newFilters[currentCategory],
          [key]: value
        };
      } else {
        // For other filters
        newFilters[currentCategory] = {
          ...newFilters[currentCategory],
          [key]: value
        };
      }
      
      return newFilters;
    });
  }, [currentCategory]);

  const getActiveFilters = useCallback(() => {
    return filters[currentCategory];
  }, [currentCategory, filters]);

  const value = {
    currentCategory,
    setCurrentCategory: setCurrentCategoryWithReset,
    filters,
    getActiveFilters,
    setFilter,
    updateFilter,
    handleCascadeClear,
    clearCategoryFilters,
    clearCurrentCategoryFilters,
    clearAllFilters
  };

  return (
    <FilterContext.Provider value={value}>
      {children}
    </FilterContext.Provider>
  );
};
