import api from "../api/apiClient";

export const OFFERING_STATUSES = {
  Draft: 0,
  Open: 1,
  Closed: 2,
  Cancelled: 3,
};

export const OFFERING_STATUS_LABELS = {
  0: "Draft",
  1: "Open",
  2: "Closed",
  3: "Cancelled",
};

export const REGISTRATION_STATES = {
  Closed: 0,
  Open: 1,
  Waitlist: 2,
};

export const REGISTRATION_STATE_LABELS = {
  0: "Closed",
  1: "Open",
  2: "Waitlist",
};

export async function fetchCourseOffering(id) {
  const { data } = await api.get(`/course-offerings/${id}`);
  return data;
}

export async function fetchOfferingsForNodeSemester(structureNodeId, semesterId, status) {
  const params = { structureNodeId, semesterId };
  if (status !== undefined && status !== null) params.status = status;
  const { data } = await api.get("/course-offerings", { params });
  return data;
}

export async function fetchOfferingsForCourse(courseId, semesterId) {
  const { data } = await api.get("/course-offerings/by-course", { params: { courseId, semesterId } });
  return data;
}

export async function createCourseOffering(body) {
  const { data } = await api.post("/course-offerings", body);
  return data;
}

export async function updateCourseOffering(id, body) {
  const { data } = await api.patch(`/course-offerings/${id}`, body);
  return data;
}
