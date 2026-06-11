import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Search, X, Check } from "lucide-react";

const MAX_LEVEL = 10;
const MAX_SEMESTER = 4;

/**
 * Shared "add a course to the plan" panel used by both the Grid and the Table
 * views. Rendered OUTSIDE any horizontally-scrolling container so the dropdown
 * list is never clipped (the old in-cell popup lived inside an overflow:auto
 * wrapper and got cut off on lower rows). Level/semester are always editable so
 * the same panel works whether opened from a specific grid cell or a free add.
 */
export default function AddCoursePanel({
  courseCatalog,
  planCourses,
  initialLevel = 1,
  initialSemester = 1,
  onAdd,
  onClose,
}) {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const [level, setLevel] = useState(initialLevel);
  const [semester, setSemester] = useState(initialSemester);
  const [isMandatory, setIsMandatory] = useState(true);
  const searchRef = useRef(null);

  // Re-sync when the caller re-targets a different cell while the panel is open.
  useEffect(() => {
    setLevel(initialLevel);
    setSemester(initialSemester);
    setSearch("");
    searchRef.current?.focus();
  }, [initialLevel, initialSemester]);

  const alreadyInPlan = useMemo(() => {
    const ids = new Set();
    for (const pc of planCourses || []) ids.add(pc.courseId);
    return ids;
  }, [planCourses]);

  const filtered = useMemo(() => {
    const list = courseCatalog || [];
    const q = search.trim().toLowerCase();
    if (!q) return list;
    return list.filter(
      (c) => c.code?.toLowerCase().includes(q) || c.title?.toLowerCase().includes(q)
    );
  }, [courseCatalog, search]);

  return (
    <div className="aplans-add-panel" role="dialog" aria-label={t("add_course_to_plan")}>
      <div className="aplans-add-panel-head">
        <span className="aplans-add-panel-title">{t("add_course_to_plan")}</span>
        <button className="aplans-add-panel-close" onClick={onClose} aria-label={t("cancel")}>
          <X size={15} />
        </button>
      </div>

      <div className="aplans-add-panel-controls">
        <label className="aplans-add-field">
          <span>{t("level")}</span>
          <select value={level} onChange={(e) => setLevel(Number(e.target.value))}>
            {Array.from({ length: MAX_LEVEL }, (_, i) => i + 1).map((l) => (
              <option key={l} value={l}>{l}</option>
            ))}
          </select>
        </label>
        <label className="aplans-add-field">
          <span>{t("semester")}</span>
          <select value={semester} onChange={(e) => setSemester(Number(e.target.value))}>
            {Array.from({ length: MAX_SEMESTER }, (_, i) => i + 1).map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </label>
        <div className="aplans-add-toggle" role="group" aria-label={t("type")}>
          <button
            type="button"
            className={isMandatory ? "active" : ""}
            onClick={() => setIsMandatory(true)}
          >
            {t("mandatory")}
          </button>
          <button
            type="button"
            className={!isMandatory ? "active" : ""}
            onClick={() => setIsMandatory(false)}
          >
            {t("elective")}
          </button>
        </div>
      </div>

      <div className="aplans-add-search">
        <Search size={14} />
        <input
          ref={searchRef}
          type="text"
          placeholder={t("search_courses")}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          autoFocus
        />
        {search && (
          <button className="aplans-add-search-clear" onClick={() => setSearch("")} aria-label={t("clear")}>
            <X size={13} />
          </button>
        )}
      </div>

      <div className="aplans-add-list">
        {filtered.length === 0 ? (
          <div className="aplans-add-empty">{t("no_courses_match")}</div>
        ) : (
          filtered.map((c) => {
            const inPlan = alreadyInPlan.has(c.id);
            return (
              <button
                key={c.id}
                type="button"
                className={`aplans-add-item ${inPlan ? "in-plan" : ""}`}
                disabled={inPlan}
                onClick={() => onAdd({ courseId: c.id, level, semester, isMandatory })}
                title={inPlan ? t("already_in_plan") : `${c.code} — ${c.title}`}
              >
                <span className="aplans-add-item-code">{c.code}</span>
                <span className="aplans-add-item-title">{c.title}</span>
                <span className="aplans-add-item-credits">{c.creditHours} {t("cr")}</span>
                {inPlan && <Check size={13} className="aplans-add-item-check" />}
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}
