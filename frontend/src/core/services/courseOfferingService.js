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
  const { data } = await api.get("/course-offerings", {
    params: { courseId, semesterId, page: 1, pageSize: 100 },
  });
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

export async function searchCourseOfferings(params = {}) {
  const { data } = await api.get("/course-offerings", { params });
  return data;
}

export async function closeCourseOffering(id) {
  const { data } = await api.post(`/course-offerings/${id}/close-record`);
  return data;
}

export async function openCourseOffering(id) {
  const { data } = await api.post(`/course-offerings/${id}/open-record`);
  return data;
}

// Batch section creation — codes generated server-side.
// body: { courseId, semesterId, structureNodeId, sectionPrefix?, count, capacity, instructorId? }
// returns: { succeeded, failed, created: [{id, sectionCode}], failures: [{sectionCode, message}] }
export async function batchCreateOfferings(body) {
  const { data } = await api.post("/course-offerings/batch", body);
  return data;
}

// Aggregate counters for the stats strip; structureNodeId optional.
export async function fetchOfferingStats(semesterId, structureNodeId) {
  const params = { semesterId };
  if (structureNodeId) params.structureNodeId = structureNodeId;
  const { data } = await api.get("/course-offerings/stats", { params });
  return data;
}

export async function bulkPublishOfferings(ids) {
  const { data } = await api.post("/course-offerings/publish", { ids });
  return data;
}

export async function bulkCancelOfferings(ids, reason) {
  const { data } = await api.post("/course-offerings/cancel", { ids, payload: { reason } });
  return data;
}
