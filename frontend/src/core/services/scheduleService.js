import api from "../api/apiClient";
import * as courseOfferingService from "./courseOfferingService";

export const SLOT_KINDS = {
  Lecture: 0,
  Lab: 1,
  Tutorial: 2,
  Seminar: 3,
  Exam: 4,
  Other: 5,
};

export const SLOT_KIND_LABELS = {
  0: "Lecture",
  1: "Lab",
  2: "Tutorial",
  3: "Seminar",
  4: "Exam",
  5: "Other",
};

export const DAY_LABELS = {
  0: "Sunday",
  1: "Monday",
  2: "Tuesday",
  3: "Wednesday",
  4: "Thursday",
  5: "Friday",
  6: "Saturday",
};

export async function fetchSlot(id) {
  const { data } = await api.get(`/schedule-slots/${id}`);
  return data;
}

export async function fetchSlotsForOffering(courseOfferingId) {
  const { data } = await api.get(`/schedule-slots/offering/${courseOfferingId}`);
  return data;
}

export async function createSlot(body) {
  const { data } = await api.post("/schedule-slots", body);
  return data;
}

export async function updateSlot(id, body) {
  const { data } = await api.patch(`/schedule-slots/${id}`, body);
  return data;
}

export async function deleteSlot(id) {
  const { data } = await api.delete(`/schedule-slots/${id}`);
  return data;
}

export async function searchSlots(params = {}) {
  const { data } = await api.get("/schedule-slots", { params });
  return data;
}

export async function batchCreateSlots(courseOfferingId, slots) {
  const { data } = await api.post("/schedule-slots/batch", { courseOfferingId, slots });
  return data;
}

export async function closeSlot(id) {
  const { data } = await api.post(`/schedule-slots/${id}/close-record`);
  return data;
}

export async function openSlot(id) {
  const { data } = await api.post(`/schedule-slots/${id}/open-record`);
  return data;
}

export async function fetchOfferingsForSchedule(structureNodeId, semesterId) {
  return courseOfferingService.fetchOfferingsForNodeSemester(structureNodeId, semesterId);
}
