import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { BookOpen, Plus, Trash2, ArrowUpDown, ArrowUp, ArrowDown, AlertTriangle } from "lucide-react";
import AddCoursePanel from "./AddCoursePanel";
import CreditSummaryBar from "./CreditSummaryBar";

const MAX_LEVEL = 10;
const MAX_SEMESTER = 4;

/**
 * Spreadsheet-style editor for a plan's composition — the Table view is now the
 * place to do precise edits the Grid can't: change a course's level/semester via
 * dropdowns, flip mandatory/elective, sort by any column, add and remove. Shares
 * the Grid's card, badges and summary bar so the two views feel like one tool.
 * Includes a prerequisites column (codes; missing-in-plan ones flagged).
 */
export default function CurriculumTable({
  planCourses,
  courseCatalog,
  prereqPairs = [],
  onAddCourse,
  onRemoveCourse,
  onUpdateCourse,
  readOnly = false,
}) {
  const { t } = useTranslation();
  const { t: ta } = useTranslation("academic");
  const [sortKey, setSortKey] = useState("level");
  const [sortDir, setSortDir] = useState("asc");
  const [showAdd, setShowAdd] = useState(false);

  const courseMap = useMemo(() => {
    const m = {};
    for (const c of courseCatalog || []) m[c.id] = c;
    return m;
  }, [courseCatalog]);

  const prereqsByCourse = useMemo(() => {
    const m = new Map();
    for (const p of prereqPairs) {
      if (!m.has(p.courseId)) m.set(p.courseId, []);
      m.get(p.courseId).push(p.prerequisiteCourseId);
    }
    return m;
  }, [prereqPairs]);

  const planCourseIds = useMemo(
    () => new Set((planCourses || []).map((pc) => pc.courseId)),
    [planCourses]
  );

  const rows = useMemo(() => {
    const list = (planCourses || []).map((pc) => {
      const c = courseMap[pc.courseId] || {};
      return {
        pc,
        code: c.code || "",
        title: c.title || "",
        credits: c.creditHours || 0,
        level: pc.level,
        semester: pc.semester,
        isMandatory: pc.isMandatory,
      };
    });
    const dir = sortDir === "asc" ? 1 : -1;
    list.sort((a, b) => {
      let av = a[sortKey];
      let bv = b[sortKey];
      if (sortKey === "code" || sortKey === "title") {
        return String(av).localeCompare(String(bv)) * dir;
      }
      if (sortKey === "type") {
        av = a.isMandatory ? 0 : 1;
        bv = b.isMandatory ? 0 : 1;
      }
      if (av !== bv) return (av - bv) * dir;
      // Stable secondary ordering keeps the table calm while editing.
      if (a.level !== b.level) return a.level - b.level;
      if (a.semester !== b.semester) return a.semester - b.semester;
      return a.code.localeCompare(b.code);
    });
    return list;
  }, [planCourses, courseMap, sortKey, sortDir]);

  const toggleSort = (key) => {
    if (sortKey === key) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortKey(key);
      setSortDir("asc");
    }
  };

  const SortHeader = ({ label, col, align }) => (
    <th
      className="aplans-ctable-th sortable"
      style={{ textAlign: align || "left" }}
      onClick={() => toggleSort(col)}
      aria-sort={sortKey === col ? (sortDir === "asc" ? "ascending" : "descending") : "none"}
    >
      <span className="aplans-ctable-th-inner" style={{ justifyContent: align === "center" ? "center" : "flex-start" }}>
        {label}
        {sortKey === col ? (
          sortDir === "asc" ? <ArrowUp size={11} /> : <ArrowDown size={11} />
        ) : (
          <ArrowUpDown size={11} className="aplans-ctable-sort-idle" />
        )}
      </span>
    </th>
  );

  const handleAdd = (data) => {
    onAddCourse?.(data);
    setShowAdd(false);
  };

  return (
    <div className="aplans-curriculum">
      {!readOnly && (
        <div className="aplans-ctable-toolbar">
          <button
            type="button"
            className="aplans-btn aplans-btn-outline aplans-ctable-add"
            onClick={() => setShowAdd((v) => !v)}
          >
            <Plus size={14} /> {t("add_course")}
          </button>
          <span className="aplans-ctable-hint">{t("table_edit_hint")}</span>
        </div>
      )}

      {!readOnly && showAdd && (
        <AddCoursePanel
          courseCatalog={courseCatalog}
          planCourses={planCourses}
          initialLevel={1}
          initialSemester={1}
          onAdd={handleAdd}
          onClose={() => setShowAdd(false)}
        />
      )}

      {rows.length === 0 ? (
        <div className="aplans-curriculum-empty">
          <BookOpen size={32} />
          <p>{t("table_empty_hint")}</p>
        </div>
      ) : (
        <div className="aplans-ctable-wrap">
          <table className="aplans-ctable" aria-label={t("plan_courses")}>
            <thead>
              <tr>
                <SortHeader label={t("code")} col="code" />
                <SortHeader label={t("title")} col="title" />
                <SortHeader label={t("credits")} col="credits" align="center" />
                <th className="aplans-ctable-th">{ta("plans.prereqColumn")}</th>
                <SortHeader label={t("level")} col="level" align="center" />
                <SortHeader label={t("semester")} col="semester" align="center" />
                <SortHeader label={t("type")} col="type" align="center" />
                {!readOnly && <th className="aplans-ctable-th" style={{ width: 44 }} />}
              </tr>
            </thead>
            <tbody>
              {rows.map(({ pc, code, title, credits }) => {
                const prereqs = prereqsByCourse.get(pc.courseId) || [];
                return (
                  <tr key={pc.id} className="aplans-ctable-row">
                    <td className="aplans-ctable-code">{code || "—"}</td>
                    <td className="aplans-ctable-title">{title || "—"}</td>
                    <td style={{ textAlign: "center" }}>{credits || "—"}</td>
                    <td>
                      {prereqs.length === 0 ? (
                        <span style={{ color: "#d1d5db" }}>—</span>
                      ) : (
                        <span className="aplans-ctable-prereqs">
                          {prereqs.map((id) => {
                            const missing = !planCourseIds.has(id);
                            const pcode = courseMap[id]?.code || "?";
                            return (
                              <span
                                key={id}
                                className={`aplans-ctable-prereq-code${missing ? " missing" : ""}`}
                                title={missing ? ta("plans.missingPrereq", { code: pcode }) : pcode}
                              >
                                {missing && <AlertTriangle size={9} />}
                                {pcode}
                              </span>
                            );
                          })}
                        </span>
                      )}
                    </td>
                    <td style={{ textAlign: "center" }}>
                      {readOnly ? (
                        pc.level
                      ) : (
                        <select
                          className="aplans-ctable-select"
                          value={pc.level}
                          onChange={(e) => onUpdateCourse?.(pc, { level: Number(e.target.value) })}
                          aria-label={t("level")}
                        >
                          {Array.from({ length: MAX_LEVEL }, (_, i) => i + 1).map((l) => (
                            <option key={l} value={l}>{l}</option>
                          ))}
                        </select>
                      )}
                    </td>
                    <td style={{ textAlign: "center" }}>
                      {readOnly ? (
                        pc.semester
                      ) : (
                        <select
                          className="aplans-ctable-select"
                          value={pc.semester}
                          onChange={(e) => onUpdateCourse?.(pc, { semester: Number(e.target.value) })}
                          aria-label={t("semester")}
                        >
                          {Array.from({ length: MAX_SEMESTER }, (_, i) => i + 1).map((s) => (
                            <option key={s} value={s}>{s}</option>
                          ))}
                        </select>
                      )}
                    </td>
                    <td style={{ textAlign: "center" }}>
                      <button
                        type="button"
                        className={`aplans-type-pill ${pc.isMandatory ? "mandatory" : "elective"}`}
                        disabled={readOnly}
                        onClick={() => !readOnly && onUpdateCourse?.(pc, { isMandatory: !pc.isMandatory })}
                        title={readOnly ? undefined : t("toggle_mandatory")}
                      >
                        {pc.isMandatory ? t("mandatory") : t("elective")}
                      </button>
                    </td>
                    {!readOnly && (
                      <td style={{ textAlign: "center" }}>
                        <button
                          type="button"
                          className="aplans-action-btn delete"
                          onClick={() => onRemoveCourse?.(pc)}
                          title={t("remove")}
                        >
                          <Trash2 size={13} />
                        </button>
                      </td>
                    )}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      <CreditSummaryBar planCourses={planCourses} courseMap={courseMap} />
    </div>
  );
}
