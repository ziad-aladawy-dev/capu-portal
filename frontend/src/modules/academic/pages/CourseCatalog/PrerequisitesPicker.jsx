import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Plus, X, AlertTriangle } from "lucide-react";
import { useToast } from "../../../../core/components/Toast";
import {
  useActiveCourses,
  useAllPrerequisitePairs,
  useCoursePrerequisites,
  useSetCoursePrerequisites,
} from "../../../../core/query/useCourses";
import { findCyclePath } from "../../utils/prereqGraph";
import shared from "../../styles/academic.module.css";
import styles from "./CourseCatalog.module.css";

/**
 * Searchable multi-select over the active catalog. Every add/remove issues a
 * batch-replace PUT; a client-side DAG walk blocks cycle-closing picks inline
 * (the server re-validates and 409s as the backstop).
 */
export default function PrerequisitesPicker({ course, readOnly }) {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const [query, setQuery] = useState("");
  const [cycleError, setCycleError] = useState(null);

  const { data: allCourses = [] } = useActiveCourses();
  const { data: pairs = [] } = useAllPrerequisitePairs();
  const { data: prereqs = [], isLoading } = useCoursePrerequisites(course?.id);
  const setPrereqs = useSetCoursePrerequisites();

  const courseById = useMemo(() => {
    const m = new Map();
    for (const c of allCourses) m.set(c.id, c);
    return m;
  }, [allCourses]);

  const currentIds = useMemo(() => prereqs.map((p) => p.prerequisiteCourseId), [prereqs]);

  const requiredByCount = useMemo(
    () => pairs.filter((p) => p.prerequisiteCourseId === course?.id).length,
    [pairs, course]
  );

  const candidates = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return [];
    const taken = new Set(currentIds);
    return allCourses
      .filter(
        (c) =>
          c.id !== course?.id &&
          !taken.has(c.id) &&
          (c.code.toLowerCase().includes(q) || (c.title || "").toLowerCase().includes(q))
      )
      .slice(0, 8);
  }, [query, allCourses, currentIds, course]);

  const persist = async (ids) => {
    try {
      await setPrereqs.mutateAsync({ courseId: course.id, prerequisiteCourseIds: ids });
      addToast(t("prereq.saved"), "success");
    } catch (err) {
      addToast(err.message || t("prereq.saveFailed"), "error");
    }
  };

  const handleAdd = (candidate) => {
    setCycleError(null);
    const proposed = [...currentIds, candidate.id];
    const cycle = findCyclePath(pairs, course.id, proposed);
    if (cycle) {
      const path = [candidate.id, ...cycle.slice(1)]
        .map((id) => courseById.get(id)?.code || id)
        .join(" → ");
      setCycleError(t("prereq.cycleError", { code: candidate.code, path: `${course.code} → ${path}` }));
      return;
    }
    setQuery("");
    persist(proposed);
  };

  const handleRemove = (prereqCourseId) => {
    setCycleError(null);
    persist(currentIds.filter((id) => id !== prereqCourseId));
  };

  if (!course) return null;

  return (
    <div>
      {requiredByCount > 0 && (
        <div className={shared.metaChip} style={{ marginBottom: 10 }}>
          {t("prereq.requiredBy", { count: requiredByCount })}
        </div>
      )}

      {!readOnly && (
        <div className={styles.pickerSearch}>
          <input
            type="text"
            className={shared.formInput}
            placeholder={t("prereq.addPlaceholder")}
            value={query}
            onChange={(e) => { setQuery(e.target.value); setCycleError(null); }}
            aria-label={t("prereq.addPlaceholder")}
          />
          {candidates.length > 0 && (
            <ul className={styles.pickerDropdown} role="listbox">
              {candidates.map((c) => (
                <li key={c.id}>
                  <button type="button" onClick={() => handleAdd(c)} disabled={setPrereqs.isPending}>
                    <Plus size={12} />
                    <span className={shared.codePill}>{c.code}</span>
                    <span className={styles.pickerTitle}>{c.title}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {cycleError && (
        <div className={shared.errorBanner} role="alert" style={{ marginTop: 8 }}>
          <AlertTriangle size={14} /> {cycleError}
        </div>
      )}

      <div className={styles.prereqList}>
        {isLoading && <div className={shared.emptyInline}>{t("common.loading")}</div>}
        {!isLoading && prereqs.length === 0 && (
          <div className={shared.emptyInline}>{t("prereq.empty")}</div>
        )}
        {prereqs.map((p) => (
          <span key={p.prerequisiteCourseId} className={styles.prereqBadge}>
            <span className={shared.codePill}>{p.code}</span>
            <span className={styles.prereqBadgeTitle}>{p.title}</span>
            <span className={styles.prereqBadgeCredits}>{p.creditHours} cr</span>
            {!readOnly && (
              <button
                type="button"
                onClick={() => handleRemove(p.prerequisiteCourseId)}
                disabled={setPrereqs.isPending}
                aria-label={t("prereq.remove")}
                title={t("prereq.remove")}
              >
                <X size={12} />
              </button>
            )}
          </span>
        ))}
      </div>
    </div>
  );
}
