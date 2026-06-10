import { create } from "zustand";
import * as academicService from "../services/academicService";

const YEAR_STORAGE_KEY = "capu_selected_academic_year";
const SEMESTER_STORAGE_KEY = "capu_selected_semester";

const useAcademicStore = create((set, get) => ({
  academicYears: [],
  semesters: [],
  selectedYear: null,
  selectedSemester: null,
  selectedYearObj: null,
  selectedSemesterObj: null,
  loading: true,

  hydrate: () => {
    try {
      const savedYear = localStorage.getItem(YEAR_STORAGE_KEY);
      const savedSem = localStorage.getItem(SEMESTER_STORAGE_KEY);
      if (savedYear) {
        const parsed = JSON.parse(savedYear);
        if (parsed?.id) parsed._saved = true;
        set({ _savedYear: parsed });
      }
      if (savedSem) {
        const parsed = JSON.parse(savedSem);
        if (parsed?.id) parsed._saved = true;
        set({ _savedSem: parsed });
      }
    } catch {}
  },

  loadAcademicYears: async () => {
    const token = localStorage.getItem("accessToken");
    if (!token) {
      set({ loading: false });
      return;
    }
    set({ loading: true });
    try {
      const years = await academicService.fetchAcademicYears();
      const list = Array.isArray(years) ? years : [];
      const state = get();
      let saved = state._savedYear;
      if (!saved) {
        try {
          const localSaved = localStorage.getItem(YEAR_STORAGE_KEY);
          if (localSaved) saved = JSON.parse(localSaved);
        } catch {}
      }
      const match = saved ? list.find((y) => y.id === saved.id) : null;
      const current = match || list.find((y) => y.isCurrent) || list[0] || null;
      set({
        academicYears: list,
        selectedYear: current?.name || "—",
        selectedYearObj: current,
        loading: false,
        _savedYear: null,
      });
      if (current) {
        localStorage.setItem(YEAR_STORAGE_KEY, JSON.stringify({ id: current.id, name: current.name }));
      }
      if (current?.id) {
        get().loadSemesters(current.id);
      }
    } catch {
      set({ academicYears: [], loading: false });
    }
  },

  loadSemesters: async (yearId) => {
    if (!yearId) {
      set({ semesters: [], selectedSemester: null, selectedSemesterObj: null });
      localStorage.removeItem(SEMESTER_STORAGE_KEY);
      return;
    }
    try {
      const sems = await academicService.fetchSemesters(yearId);
      const list = Array.isArray(sems) ? sems : [];
      const state = get();
      let saved = state._savedSem;
      if (!saved) {
        try {
          const localSaved = localStorage.getItem(SEMESTER_STORAGE_KEY);
          if (localSaved) saved = JSON.parse(localSaved);
        } catch {}
      }
      const match = saved ? list.find((s) => s.id === saved.id) : null;
      const current = match || list.find((s) => s.isCurrent) || list[0] || null;
      set({
        semesters: list,
        selectedSemester: current?.name || "—",
        selectedSemesterObj: current,
        _savedSem: null,
      });
      if (current) {
        localStorage.setItem(SEMESTER_STORAGE_KEY, JSON.stringify({ id: current.id, name: current.name }));
      }
    } catch {
      set({ semesters: [], selectedSemester: null, selectedSemesterObj: null });
    }
  },

  selectYear: (yearNameOrObj) => {
    const state = get();
    if (yearNameOrObj === null) {
      set({ selectedYear: null, selectedYearObj: null, selectedSemester: null, selectedSemesterObj: null, semesters: [] });
      localStorage.removeItem(YEAR_STORAGE_KEY);
      localStorage.removeItem(SEMESTER_STORAGE_KEY);
      return;
    }
    const found = typeof yearNameOrObj === "string"
      ? state.academicYears.find((y) => y.name === yearNameOrObj)
      : yearNameOrObj;
    if (found) {
      set({ selectedYear: found.name, selectedYearObj: found });
      localStorage.setItem(YEAR_STORAGE_KEY, JSON.stringify({ id: found.id, name: found.name }));
      get().loadSemesters(found.id);
    }
  },

  selectSemester: (semNameOrObj) => {
    const state = get();
    if (semNameOrObj === null) {
      set({ selectedSemester: null, selectedSemesterObj: null });
      localStorage.removeItem(SEMESTER_STORAGE_KEY);
      return;
    }
    const found = typeof semNameOrObj === "string"
      ? state.semesters.find((s) => s.name === semNameOrObj)
      : semNameOrObj;
    if (found) {
      set({ selectedSemester: found.name, selectedSemesterObj: found });
      localStorage.setItem(SEMESTER_STORAGE_KEY, JSON.stringify({ id: found.id, name: found.name }));
    }
  },

  clearYear: () => {
    set({ selectedYear: null, selectedYearObj: null, selectedSemester: null, selectedSemesterObj: null, semesters: [] });
    localStorage.removeItem(YEAR_STORAGE_KEY);
    localStorage.removeItem(SEMESTER_STORAGE_KEY);
  },

  clearSemester: () => {
    set({ selectedSemester: null, selectedSemesterObj: null });
    localStorage.removeItem(SEMESTER_STORAGE_KEY);
  },

  refreshAcademicYears: () => {
    get().loadAcademicYears();
  },
}));

export default useAcademicStore;
