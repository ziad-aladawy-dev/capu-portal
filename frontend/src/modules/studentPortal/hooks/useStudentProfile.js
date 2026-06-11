import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../../../core/auth/useAuth";
import * as studentService from "../../../core/services/studentService";
import {
  fetchProfileRecords,
  upsertProfileRecord,
  STUDENT_PROFILE_CATEGORY,
} from "../../../core/services/studentProfileService";
import { BLOCKER_QUERY_KEY } from "../../../core/hooks/useBlockerState";

export const CONTACT_RECORD_KEY = "contact-information";

const PROFILE_KEY = (id) => ["portal", "profile", id];

function safeParse(json) {
  try { return json ? JSON.parse(json) : {}; } catch { return {}; }
}

/**
 * Student record + the two portal-managed profile records (contact extras,
 * emergency contact). The Student entity has NO address/city/country columns —
 * those live in a Custom profile record keyed CONTACT_RECORD_KEY; guardian
 * fields live on the entity itself.
 */
export function useStudentProfile() {
  const { user } = useAuth();
  const studentId = user?.id;

  return useQuery({
    queryKey: PROFILE_KEY(studentId),
    enabled: Boolean(studentId),
    staleTime: 30_000,
    queryFn: async () => {
      const [student, recordsResult] = await Promise.all([
        studentService.fetchStudentById(studentId),
        fetchProfileRecords(studentId).catch(() => []), // tolerate 403 until grants land
      ]);
      const records = Array.isArray(recordsResult) ? recordsResult : recordsResult?.items || [];
      const emergencyRecord = records.find((r) => r.category === STUDENT_PROFILE_CATEGORY.EmergencyContact);
      const contactRecord = records.find(
        (r) => r.category === STUDENT_PROFILE_CATEGORY.Custom && r.customKey === CONTACT_RECORD_KEY
      );
      return {
        student,
        records,
        emergency: safeParse(emergencyRecord?.dataJson),
        contact: safeParse(contactRecord?.dataJson),
      };
    },
  });
}

/**
 * Partial student update (backend treats omitted fields as "keep current").
 * Optimistically merges the patch into the cached student.
 */
export function useUpdateStudent() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const studentId = user?.id;

  return useMutation({
    mutationFn: (patch) => studentService.updateStudent(studentId, patch),
    onMutate: async (patch) => {
      await qc.cancelQueries({ queryKey: PROFILE_KEY(studentId) });
      const previous = qc.getQueryData(PROFILE_KEY(studentId));
      qc.setQueryData(PROFILE_KEY(studentId), (old) =>
        old ? { ...old, student: { ...old.student, ...patch } } : old
      );
      return { previous };
    },
    onError: (_err, _patch, ctx) => {
      if (ctx?.previous) qc.setQueryData(PROFILE_KEY(studentId), ctx.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: PROFILE_KEY(studentId) });
      qc.invalidateQueries({ queryKey: BLOCKER_QUERY_KEY(studentId) });
    },
  });
}

/** Upsert one of the portal-managed profile records (emergency / contact extras). */
export function useUpsertProfileRecord() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const studentId = user?.id;

  return useMutation({
    mutationFn: ({ category, customKey, data, isSensitive = false }) =>
      upsertProfileRecord(studentId, {
        category,
        customKey,
        schemaVersion: 1,
        isSensitive,
        dataJson: JSON.stringify(data),
      }),
    onMutate: async ({ category, customKey, data }) => {
      await qc.cancelQueries({ queryKey: PROFILE_KEY(studentId) });
      const previous = qc.getQueryData(PROFILE_KEY(studentId));
      qc.setQueryData(PROFILE_KEY(studentId), (old) => {
        if (!old) return old;
        if (category === STUDENT_PROFILE_CATEGORY.EmergencyContact) {
          return { ...old, emergency: { ...old.emergency, ...data } };
        }
        if (customKey === CONTACT_RECORD_KEY) {
          return { ...old, contact: { ...old.contact, ...data } };
        }
        return old;
      });
      return { previous };
    },
    onError: (_err, _vars, ctx) => {
      if (ctx?.previous) qc.setQueryData(PROFILE_KEY(studentId), ctx.previous);
    },
    onSettled: () => {
      qc.invalidateQueries({ queryKey: PROFILE_KEY(studentId) });
      qc.invalidateQueries({ queryKey: BLOCKER_QUERY_KEY(studentId) });
    },
  });
}

/** Upload a validated profile photo, then refresh the cached record. */
export function useUploadStudentPhoto() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const studentId = user?.id;

  return useMutation({
    mutationFn: (file) => studentService.uploadStudentPhoto(studentId, file),
    onSettled: () => {
      qc.invalidateQueries({ queryKey: PROFILE_KEY(studentId) });
    },
  });
}
