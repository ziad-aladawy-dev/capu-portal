import api from "../api/apiClient";

/**
 * Admin/advisor-scoped reads of a SPECIFIC student's academic record.
 * These mirror the student-self DTOs (TranscriptDto, AcademicSummaryDto,
 * SemesterHistoryDto, RegisteredCourseDto) but are keyed by route studentId
 * and guarded by admin permissions.
 *
 * Backend contract (to be added):
 *   GET /api/students/{id}/transcript        -> TranscriptDto
 *   GET /api/students/{id}/grades/summary    -> AcademicSummaryDto
 *   GET /api/students/{id}/grades/history    -> SemesterHistoryDto[]
 *   GET /api/students/{id}/registered        -> RegisteredCourseDto[]
 *
 * Until these exist the calls 404; callers treat that as a "pending endpoint" state.
 */
export async function fetchStudentTranscript(studentId) {
  const { data } = await api.get(`/students/${studentId}/transcript`);
  return data;
}

export async function fetchStudentGradeSummary(studentId) {
  const { data } = await api.get(`/students/${studentId}/grades/summary`);
  return data;
}

export async function fetchStudentGradeHistory(studentId) {
  const { data } = await api.get(`/students/${studentId}/grades/history`);
  return data;
}

export async function fetchStudentRegistered(studentId) {
  const { data } = await api.get(`/students/${studentId}/registered`);
  return data;
}

export const REGISTRATION_STATUS_LABELS = {
  0: "Enrolled",
  1: "Completed",
  2: "Withdrawn",
  3: "Failed",
  4: "Cancelled",
};

export const ACADEMIC_RESULT_STATUS_LABELS = {
  0: "In Progress",
  1: "Passed",
  2: "Failed",
  3: "Withdrawn",
  4: "Incomplete",
};
