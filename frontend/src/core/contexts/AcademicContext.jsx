import { createContext, useContext, useState, useCallback, useEffect } from "react";
import { useAuth } from "../auth/useAuth";
import * as academicService from "../services/academicService";

const AcademicContext = createContext(null);

export function AcademicProvider({ children }) {
  const { activeScope, isAuthenticated } = useAuth();

  const [selectedYear, setSelectedYear] = useState("2025-2026");
  const [selectedSemester, setSelectedSemester] = useState("Fall Semester");
  const [selectedYearId, setSelectedYearId] = useState(null);
  const [selectedSemesterId, setSelectedSemesterId] = useState(null);

  const [academicYears, setAcademicYears] = useState([]);
  const [semesters, setSemesters] = useState([]);
  const [yearsLoading, setYearsLoading] = useState(false);
  const [semestersLoading, setSemestersLoading] = useState(false);

  useEffect(() => {
    if (!isAuthenticated) return;
    setYearsLoading(true);
    academicService.fetchAcademicYears()
      .then((data) => {
        const years = Array.isArray(data) ? data : [];
        setAcademicYears(years);
        if (years.length > 0) {
          const current = years.find((y) => y.isCurrent) || years[0];
          setSelectedYear(current.name);
          setSelectedYearId(current.id);
        }
      })
      .catch(() => {
        setAcademicYears([]);
      })
      .finally(() => setYearsLoading(false));
  }, [isAuthenticated]);

  useEffect(() => {
    if (!activeScope?.temporal?.academicYearId || !academicYears.length) return;
    const match = academicYears.find((y) => y.id === activeScope.temporal.academicYearId);
    if (match) {
      setSelectedYear(match.name);
      setSelectedYearId(match.id);
    }
    if (activeScope.temporal.semesterId) {
      setSelectedSemesterId(activeScope.temporal.semesterId);
    }
  }, [activeScope, academicYears]);

  useEffect(() => {
    if (!selectedYearId) return;
    setSemestersLoading(true);
    academicService.fetchSemesters(selectedYearId)
      .then((data) => {
        const sems = Array.isArray(data) ? data : [];
        setSemesters(sems);
        if (sems.length > 0) {
          const match = selectedSemesterId
            ? sems.find((s) => s.id === selectedSemesterId)
            : null;
          const current = match || sems.find((s) => s.isCurrent) || sems[0];
          setSelectedSemester(current.name);
          setSelectedSemesterId(current.id);
        }
      })
      .catch(() => {
        setSemesters([]);
      })
      .finally(() => setSemestersLoading(false));
  }, [selectedYearId]);

  const selectYear = useCallback((year) => {
    const match = academicYears.find((y) => y.name === year);
    setSelectedYear(year);
    setSelectedYearId(match?.id || null);
  }, [academicYears]);

  const selectSemester = useCallback((semester) => {
    const match = semesters.find((s) => s.name === semester);
    setSelectedSemester(semester);
    setSelectedSemesterId(match?.id || null);
  }, [semesters]);

  const value = {
    selectedYear,
    selectedSemester,
    selectedYearId,
    selectedSemesterId,
    academicYears,
    semesters,
    yearsLoading,
    semestersLoading,
    selectYear,
    selectSemester,
  };

  return (
    <AcademicContext.Provider value={value}>
      {children}
    </AcademicContext.Provider>
  );
}

export function useAcademic() {
  const context = useContext(AcademicContext);
  if (!context) {
    throw new Error("useAcademic must be used within an AcademicProvider");
  }
  return context;
}
