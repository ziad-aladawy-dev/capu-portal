import { useState, useCallback, useEffect } from "react";
import * as courseOfferingService from "../../../core/services/courseOfferingService";
import * as courseService from "../../../core/services/courseService";
import * as academicService from "../../../core/services/academicService";
import * as structureService from "../../../core/services/structureService";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";

export function useCourseOfferings() {
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();

  const [offerings, setOfferings] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const [courses, setCourses] = useState([]);
  const [faculties, setFaculties] = useState([]);
  const [semesterId, setSemesterId] = useState(null);

  const fetchDependencies = useCallback(async () => {
    try {
      const [courseData, facultyData] = await Promise.all([
        courseService.fetchActiveCourses(),
        structureService.fetchFaculties(),
      ]);
      setCourses(Array.isArray(courseData) ? courseData : []);
      setFaculties(Array.isArray(facultyData) ? facultyData : []);
    } catch { }
  }, []);

  useEffect(() => {
    fetchDependencies();
  }, [fetchDependencies]);

  useEffect(() => {
    if (selectedSemesterObj?.id) {
      setSemesterId(selectedSemesterObj.id);
    }
  }, [selectedSemesterObj]);

  const loadOfferings = useCallback(async (nodeId, semId, statusFilter) => {
    if (!nodeId || !semId) {
      setOfferings([]);
      return;
    }
    setLoading(true);
    setError(null);
    try {
      const data = await courseOfferingService.fetchOfferingsForNodeSemester(
        nodeId, semId, statusFilter
      );
      setOfferings(Array.isArray(data) ? data : []);
    } catch (err) {
      setError(err.message || "Failed to load offerings");
      setOfferings([]);
    } finally {
      setLoading(false);
    }
  }, []);

  const createOffering = useCallback(async (body) => {
    const result = await courseOfferingService.createCourseOffering(body);
    return result;
  }, []);

  const updateOffering = useCallback(async (id, body) => {
    const result = await courseOfferingService.updateCourseOffering(id, body);
    return result;
  }, []);

  return {
    offerings,
    loading,
    error,
    courses,
    faculties,
    semesterId,
    loadOfferings,
    createOffering,
    updateOffering,
    setError,
  };
}
