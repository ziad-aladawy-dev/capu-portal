import { createContext, useContext, useState, useEffect, useCallback } from "react";
import * as academicService from "../services/academicService";

const AcademicContext = createContext();

const YEAR_STORAGE_KEY = "capu_selected_academic_year";
const SEMESTER_STORAGE_KEY = "capu_selected_semester";

export const AcademicProvider = ({ children }) => {
  const [academicYears, setAcademicYears] = useState([]);
  const [semesters, setSemesters] = useState([]);
  const [selectedYear, setSelectedYear] = useState(null);
  const [selectedSemester, setSelectedSemester] = useState(null);
  const [loading, setLoading] = useState(true);

  const saveYear = useCallback((year) => {
    if (year?.id) {
      localStorage.setItem(YEAR_STORAGE_KEY, JSON.stringify({ id: year.id, name: year.name }));
    } else {
      localStorage.removeItem(YEAR_STORAGE_KEY);
    }
  }, []);

  const saveSemester = useCallback((sem) => {
    if (sem?.id) {
      localStorage.setItem(SEMESTER_STORAGE_KEY, JSON.stringify({ id: sem.id, name: sem.name }));
    } else {
      localStorage.removeItem(SEMESTER_STORAGE_KEY);
    }
  }, []);

  // Restore saved selections on mount
  useEffect(() => {
    try {
      const savedYear = localStorage.getItem(YEAR_STORAGE_KEY);
      const savedSem = localStorage.getItem(SEMESTER_STORAGE_KEY);
      // We'll apply them after the API data loads — just mark them for now
      if (savedYear) {
        const parsed = JSON.parse(savedYear);
        if (parsed?.id) parsed._saved = true;
        // Stash on window so the fetch handler can read it
        window.__savedYear = parsed?.id ? parsed : null;
      }
      if (savedSem) {
        const parsed = JSON.parse(savedSem);
        if (parsed?.id) parsed._saved = true;
        window.__savedSem = parsed?.id ? parsed : null;
      }
    } catch {}
  }, []);

  useEffect(() => {
    const token = localStorage.getItem("accessToken");
    if (!token) {
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    academicService
      .fetchAcademicYears()
      .then((years) => {
        if (cancelled) return;
        const list = Array.isArray(years) ? years : [];
        setAcademicYears(list);
        // Prefer saved year, fall back to current or first
        const saved = window.__savedYear;
        const match = saved
          ? list.find((y) => y.id === saved.id)
          : null;
        const current = match || list.find((y) => y.isCurrent) || list[0] || null;
        if (current) {
          setSelectedYear(current);
          saveYear(current);
        }
        window.__savedYear = null;
      })
      .catch(() => {
        if (!cancelled) setAcademicYears([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, [saveYear]);

  useEffect(() => {
    if (!selectedYear?.id) {
      setSemesters([]);
      setSelectedSemester(null);
      saveSemester(null);
      return;
    }
    let cancelled = false;
    academicService
      .fetchSemesters(selectedYear.id)
      .then((sems) => {
        if (cancelled) return;
        const list = Array.isArray(sems) ? sems : [];
        setSemesters(list);
        // Prefer saved semester, fall back to current or first
        const saved = window.__savedSem;
        const match = saved
          ? list.find((s) => s.id === saved.id)
          : null;
        const current = match || list.find((s) => s.isCurrent) || list[0] || null;
        setSelectedSemester(current);
        saveSemester(current);
        window.__savedSem = null;
      })
      .catch(() => {
        if (!cancelled) {
          setSemesters([]);
          setSelectedSemester(null);
          saveSemester(null);
        }
      });
    return () => { cancelled = true; };
  }, [selectedYear, saveSemester]);

  const selectYear = useCallback(
    (yearNameOrObj) => {
      const found =
        typeof yearNameOrObj === "string"
          ? academicYears.find((y) => y.name === yearNameOrObj)
          : yearNameOrObj;
      if (found) {
        setSelectedYear(found);
        saveYear(found);
      }
    },
    [academicYears, saveYear]
  );

  const selectSemester = useCallback(
    (semNameOrObj) => {
      const found =
        typeof semNameOrObj === "string"
          ? semesters.find((s) => s.name === semNameOrObj)
          : semNameOrObj;
      if (found) {
        setSelectedSemester(found);
        saveSemester(found);
      }
    },
    [semesters, saveSemester]
  );

  return (
    <AcademicContext.Provider
      value={{
        academicYears,
        semesters,
        selectedYear: selectedYear?.name || "—",
        selectedYearObj: selectedYear,
        selectedSemester: selectedSemester?.name || "—",
        selectedSemesterObj: selectedSemester,
        selectYear,
        selectSemester,
        loading,
      }}
    >
      {children}
    </AcademicContext.Provider>
  );
};

export const useAcademic = () => useContext(AcademicContext);