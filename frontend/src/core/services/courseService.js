import api from "../api/apiClient";

export const COURSE_CATEGORIES = [
  { value: 0, label: "Unspecified" },
  { value: 1, label: "Program Requirement" },
  { value: 2, label: "University Requirement" },
  { value: 3, label: "Faculty Requirement" },
  { value: 4, label: "Elective" },
  { value: 5, label: "General Education" },
];

export function getCourseCategoryLabel(value) {
  return COURSE_CATEGORIES.find((c) => c.value === value)?.label || "Unspecified";
}

export async function fetchActiveCourses() {
  return api.get("/courses");
}

export async function fetchCourse(id) {
  return api.get(`/courses/${id}`);
}

export async function createCourse(data) {
  return api.post("/courses", data);
}

export async function updateCourse(id, data) {
  return api.patch(`/courses/${id}`, data);
}

export async function deleteCourse(id) {
  return api.delete(`/courses/${id}`);
}
