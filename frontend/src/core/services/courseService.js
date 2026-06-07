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
  const { data } = await api.get("/courses");
  return data;
}

export async function fetchCourse(id) {
  const { data } = await api.get(`/courses/${id}`);
  return data;
}

export async function createCourse(body) {
  const { data } = await api.post("/courses", body);
  return data;
}

export async function updateCourse(id, body) {
  const { data } = await api.patch(`/courses/${id}`, body);
  return data;
}

export async function deleteCourse(id) {
  const { data } = await api.delete(`/courses/${id}`);
  return data;
}
