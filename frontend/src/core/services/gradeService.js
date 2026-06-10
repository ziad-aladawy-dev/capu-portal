import api from "../api/apiClient";

// AcademicResultStatus (read-only, synced).
export const ACADEMIC_RESULT_STATUS = { InProgress: 0, Passed: 1, Failed: 2, Withdrawn: 3 };

/** Academic summary: GPA / CGPA / credits / standing. Returns null if unavailable. */
export async function fetchGradeSummary() {
  try {
    const { data } = await api.get("/grades/summary");
    return data || null;
  } catch {
    return null;
  }
}

/** Grade history grouped by semester (latest → oldest). */
export async function fetchGradeHistory() {
  const { data } = await api.get("/grades/history");
  return Array.isArray(data) ? data : data?.items || [];
}

/** Detailed grades for a single semester. */
export async function fetchSemesterGrades(semesterId) {
  const { data } = await api.get(`/grades/history/${semesterId}`);
  return Array.isArray(data) ? data : data?.items || [];
}

// ── GPA helpers (client-side projection / what-if) ───────────────────────────
// Standard 4.0 letter→points scale. Used only for the projection calculator;
// the authoritative GPA comes from /grades/summary.
export const GRADE_POINTS = {
  "A+": 4.0, A: 4.0, "A-": 3.7,
  "B+": 3.3, B: 3.0, "B-": 2.7,
  "C+": 2.3, C: 2.0, "C-": 1.7,
  "D+": 1.3, D: 1.0, "D-": 0.7,
  F: 0.0,
};

export const GRADE_OPTIONS = Object.keys(GRADE_POINTS);

/**
 * Weighted GPA from [{ credits, grade }]. Ignores rows without a recognised
 * letter grade (in-progress / withdrawn).
 */
export function computeGpa(rows) {
  let points = 0;
  let credits = 0;
  for (const r of rows || []) {
    const gp = GRADE_POINTS[r.grade];
    const c = Number(r.credits) || 0;
    if (gp == null || c === 0) continue;
    points += gp * c;
    credits += c;
  }
  return credits === 0 ? 0 : points / credits;
}

/**
 * Projects a new CGPA given prior earned credits + CGPA and a set of planned
 * { credits, grade } rows for in-progress courses.
 */
export function projectCgpa({ priorCredits = 0, priorCgpa = 0, plannedRows = [] }) {
  const planned = plannedRows.filter((r) => GRADE_POINTS[r.grade] != null && Number(r.credits) > 0);
  const plannedPoints = planned.reduce((s, r) => s + GRADE_POINTS[r.grade] * Number(r.credits), 0);
  const plannedCredits = planned.reduce((s, r) => s + Number(r.credits), 0);
  const totalCredits = priorCredits + plannedCredits;
  if (totalCredits === 0) return 0;
  return (priorCgpa * priorCredits + plannedPoints) / totalCredits;
}
