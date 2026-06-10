import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../auth/useAuth";
import * as studentService from "../services/studentService";
import {
  fetchProfileRecords,
  STUDENT_PROFILE_CATEGORY,
} from "../services/studentProfileService";

/**
 * Required-profile configuration that drives the completeness gate.
 *
 * Phase 2 scope: a student must have core contact details (phone + address)
 * on file AND an emergency contact (name + phone) before reaching the
 * dashboard. Adjust this list to change what blocks access — the wizard reads
 * the same definition.
 */
export const REQUIRED_PROFILE_FIELDS = [
  { key: "phoneNumber", label: "Phone Number", severity: "high", source: "core" },
  { key: "address", label: "Permanent Address", severity: "high", source: "core" },
  { key: "emergencyName", label: "Emergency Contact Name", severity: "high", source: "emergency" },
  { key: "emergencyPhone", label: "Emergency Contact Phone", severity: "high", source: "emergency" },
];

const EMERGENCY_CATEGORY = STUDENT_PROFILE_CATEGORY.EmergencyContact;

function safeParse(json) {
  try {
    return json ? JSON.parse(json) : {};
  } catch {
    return {};
  }
}

function hasText(v) {
  return typeof v === "string" && v.trim().length > 0;
}

/**
 * Reads the current student's core record + profile records and computes
 * which required fields are still missing. Returns the raw pieces too, so the
 * wizard can prefill what already exists.
 */
export function buildCompleteness(student, records) {
  const emergency = (records || []).find((r) => r.category === EMERGENCY_CATEGORY);
  const emergencyData = safeParse(emergency?.dataJson);

  const values = {
    phoneNumber: student?.phoneNumber,
    address: student?.address,
    emergencyName: emergencyData.name,
    emergencyPhone: emergencyData.phone,
  };

  const missingFields = REQUIRED_PROFILE_FIELDS.filter((f) => !hasText(values[f.key]));
  const filled = REQUIRED_PROFILE_FIELDS.length - missingFields.length;
  const overallPercentage = Math.round((filled / REQUIRED_PROFILE_FIELDS.length) * 100);

  return { overallPercentage, missingFields, emergencyRecord: emergency || null, emergencyData };
}

export const BLOCKER_QUERY_KEY = (studentId) => ["blocker-state", studentId];

/**
 * Aggregates everything the StudentBlockerGate needs to decide whether to let
 * the student through. Fails OPEN on data errors (never locks a student out of
 * the app because a profile request hiccuped).
 */
export function useBlockerState() {
  const { user, requiresPasswordChange, isAuthenticated } = useAuth();
  const studentId = user?.id;
  const enabled = Boolean(isAuthenticated && studentId);

  const query = useQuery({
    queryKey: BLOCKER_QUERY_KEY(studentId),
    enabled,
    queryFn: async () => {
      const [student, recordsResult] = await Promise.all([
        studentService.fetchStudentById(studentId),
        fetchProfileRecords(studentId),
      ]);
      const records = Array.isArray(recordsResult)
        ? recordsResult
        : recordsResult?.items || [];
      return { student, records, ...buildCompleteness(student, records) };
    },
    staleTime: 30_000,
  });

  const completeness = query.data;

  return {
    isLoading: enabled && query.isLoading,
    isError: query.isError,
    requiresPasswordChange: Boolean(requiresPasswordChange),
    // Fail open: when disabled (no student id) or the profile request errored,
    // never trap the user behind the wizard.
    profileCompleteness: !enabled || query.isError ? 100 : completeness?.overallPercentage ?? 0,
    missingFields: completeness?.missingFields ?? [],
    student: completeness?.student ?? null,
    emergencyData: completeness?.emergencyData ?? {},
    // No mandatory-actions subsystem exists yet (Phase 2 deferred). Always
    // returns "none pending" so the gate degrades gracefully.
    hasPendingMandatoryAction: false,
  };
}
