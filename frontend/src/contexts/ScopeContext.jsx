import React, { createContext, useState, useCallback } from "react";
import {
  mockActiveScope,
  mockAuthorizedScope,
  mockColleges,
  mockPrograms,
  mockAcademicYears,
  mockSemesters
} from "../lib/mock-data";

export const ScopeContext = createContext();

export const ScopeProvider = ({ children }) => {
  const [activeScope, setActiveScope] = useState(mockActiveScope);
  const [authorizedScope] = useState(mockAuthorizedScope);

  // Get options for dropdowns based on authorized scope
  const getFacultyOptions = useCallback(() => {
    return mockColleges.filter(c =>
      authorizedScope.allowedFacultyIds.includes(c.id)
    );
  }, [authorizedScope.allowedFacultyIds]);

  const getProgramOptions = useCallback((facultyId = null) => {
    let programs = mockPrograms.filter(p =>
      authorizedScope.allowedProgramIds.includes(p.id)
    );
    // Filter by faculty if provided
    if (facultyId) {
      programs = programs.filter(p => p.collegeId === facultyId);
    }
    return programs;
  }, [authorizedScope.allowedProgramIds]);

  const getAcademicYearOptions = useCallback(() => {
    return mockAcademicYears.filter(y =>
      authorizedScope.allowedAcademicYearIds.includes(y.id)
    );
  }, [authorizedScope.allowedAcademicYearIds]);

  const getSemesterOptions = useCallback(() => {
    return mockSemesters.filter(s =>
      authorizedScope.allowedSemesterIds.includes(s.id)
    );
  }, [authorizedScope.allowedSemesterIds]);

  const getCurrentFacultyName = useCallback(() => {
    const faculty = mockColleges.find(c => c.id === activeScope.structural.facultyId);
    return faculty ? faculty.name : "Select Faculty";
  }, [activeScope.structural.facultyId]);

  const getCurrentSemesterDisplay = useCallback(() => {
    const year = mockAcademicYears.find(y => y.id === activeScope.temporal.academicYearId);
    const semester = mockSemesters.find(s => s.id === activeScope.temporal.semesterId);
    if (year && semester) {
      return `${semester.name} ${year.name}`;
    }
    return "Select Semester";
  }, [activeScope.temporal.academicYearId, activeScope.temporal.semesterId]);

  const updateActiveScope = useCallback((updates) => {
    setActiveScope(prev => ({
      ...prev,
      ...updates
    }));
  }, []);

  const updateStructuralScope = useCallback((facultyId, programId) => {
    setActiveScope(prev => ({
      ...prev,
      structural: {
        facultyId,
        programId
      }
    }));
  }, []);

  const updateTemporalScope = useCallback((academicYearId, semesterId) => {
    setActiveScope(prev => ({
      ...prev,
      temporal: {
        academicYearId,
        semesterId
      }
    }));
  }, []);

  // Combined updateScope for ScopeEditor
  const updateScope = useCallback(({ colleges, academicYear, semester }) => {
    setActiveScope(prev => ({
      ...prev,
      structural: {
        facultyId: colleges && colleges.length > 0 ? colleges[0] : prev.structural?.facultyId,
        programId: null
      },
      temporal: {
        academicYearId: academicYear || prev.temporal?.academicYearId,
        semesterId: semester || prev.temporal?.semesterId
      }
    }));
  }, []);

  const value = {
    activeScope,
    authorizedScope,
    getFacultyOptions,
    getProgramOptions,
    getAcademicYearOptions,
    getSemesterOptions,
    getCurrentFacultyName,
    getCurrentSemesterDisplay,
    updateActiveScope,
    updateStructuralScope,
    updateTemporalScope,
    updateScope
  };

  return (
    <ScopeContext.Provider value={value}>
      {children}
    </ScopeContext.Provider>
  );
};
