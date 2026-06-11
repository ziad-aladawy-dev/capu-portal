import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { BookOpen, Plus, X, AlertTriangle } from "lucide-react";
import AddCoursePanel from "./AddCoursePanel";
import CreditSummaryBar from "./CreditSummaryBar";

const MAX_LEVEL = 10;
const MAX_SEMESTER = 4;

// A term (one level × semester cell) carrying 12–18 credits is a normal load;
// 10–11 / 19–21 borderline; anything beyond is flagged.
const creditTone = (credits) => {
  if (credits >= 12 && credits <= 18) return "ok";
  if ((credits >= 10 && credits <= 11) || (credits >= 19 && credits <= 21)) return "warn";
  return "danger";
};

/**
 * Visual curriculum matrix (levels × semesters). Supports the full CRUD surface:
 *   - Create: "+" on any cell opens the shared AddCoursePanel pre-targeted to it
 *   - Read:   course badges per cell, mandatory/elective colour-coded
 *   - Update: click the R/E pill to flip mandatory; drag a badge to move it
 *   - Delete: the × on a badge
 *
 * Prerequisite awareness: a badge shows a chain count when its course has
 * prerequisites; a warning marker when one of those prerequisites is not
 * placed anywhere in the plan. Per-cell credit chips colour-code term load.
 */
function CurriculumGrid({
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
  const [target, setTarget] = useState(null); // { level, semester } cell being added to
  const [dragPc, setDragPc] = useState(null);
  const [dragOver, setDragOver] = useState(null); // "level-semester"

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

  const grid = useMemo(() => {
    const g = {};
    for (let l = 1; l <= MAX_LEVEL; l++) {
      for (let s = 1; s <= MAX_SEMESTER; s++) g[`${l}-${s}`] = [];
    }
    for (const pc of planCourses || []) {
      const key = `${pc.level}-${pc.semester}`;
      if (g[key]) g[key].push(pc);
    }
    return g;
  }, [planCourses]);

  const cellCredits = (key) =>
    (grid[key] || []).reduce((a, pc) => a + (courseMap[pc.courseId]?.creditHours || 0), 0);

  const columnTotals = useMemo(() => {
    const totals = Array.from({ length: MAX_SEMESTER }, () => 0);
    for (const pc of planCourses || []) {
      const idx = pc.semester - 1;
      if (idx >= 0 && idx < MAX_SEMESTER) totals[idx] += courseMap[pc.courseId]?.creditHours || 0;
    }
    return totals;
  }, [planCourses, courseMap]);

  const isLevelEmpty = (level) => {
    for (let s = 1; s <= MAX_SEMESTER; s++) {
      if (grid[`${level}-${s}`].length > 0) return false;
    }
    return true;
  };

  const handleAdd = (data) => {
    onAddCourse?.(data);
    setTarget(null);
  };

  const handleDrop = (level, semester) => {
    setDragOver(null);
    const pc = dragPc;
    setDragPc(null);
    if (!pc || readOnly) return;
    if (pc.level === level && pc.semester === semester) return;
    onUpdateCourse?.(pc, { level, semester });
  };

  const prereqInfo = (courseId) => {
    const prereqs = prereqsByCourse.get(courseId) || [];
    if (!prereqs.length) return null;
    const missing = prereqs.filter((id) => !planCourseIds.has(id));
    const codes = prereqs.map((id) => courseMap[id]?.code || "?").join(", ");
    return { count: prereqs.length, missing, codes };
  };

  return (
    <div className="aplans-curriculum">
      <div className="aplans-curriculum-grid-wrap">
        <table className="aplans-grid-table" aria-label={t("plan_courses")}>
          <thead>
            <tr>
              <th className="aplans-grid-corner">{t("level")}</th>
              {Array.from({ length: MAX_SEMESTER }, (_, i) => (
                <th key={i} className="aplans-grid-sem-header">
                  {t("semester")} {i + 1}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: MAX_LEVEL }, (_, li) => {
              const level = li + 1;
              return (
                <tr key={level} className={`aplans-grid-row ${isLevelEmpty(level) ? "empty-level" : ""}`}>
                  <td className="aplans-grid-level-cell">
                    <span className="aplans-grid-level-num">{level}</span>
                  </td>
                  {Array.from({ length: MAX_SEMESTER }, (_, si) => {
                    const semester = si + 1;
                    const key = `${level}-${semester}`;
                    const courses = grid[key] || [];
                    const isTarget = target?.level === level && target?.semester === semester;
                    const isDragOver = dragOver === key;
                    const credits = cellCredits(key);
                    return (
                      <td
                        key={semester}
                        className={`aplans-grid-cell ${courses.length === 0 ? "empty" : ""} ${isTarget ? "is-target" : ""} ${isDragOver ? "is-dragover" : ""}`}
                        onDragOver={(e) => {
                          if (readOnly || !dragPc) return;
                          e.preventDefault();
                          e.dataTransfer.dropEffect = "move";
                          if (dragOver !== key) setDragOver(key);
                        }}
                        onDragLeave={() => setDragOver((cur) => (cur === key ? null : cur))}
                        onDrop={() => handleDrop(level, semester)}
                      >
                        {courses.length > 0 ? (
                          <div className="aplans-grid-cell-courses">
                            {courses.map((pc) => {
                              const course = courseMap[pc.courseId];
                              const info = prereqInfo(pc.courseId);
                              const missingTitle = info?.missing.length
                                ? info.missing
                                    .map((id) => ta("plans.missingPrereq", { code: courseMap[id]?.code || id }))
                                    .join("\n")
                                : null;
                              return (
                                <div
                                  key={pc.id}
                                  className={`aplans-grid-course-badge ${pc.isMandatory ? "mandatory" : "elective"} ${dragPc?.id === pc.id ? "dragging" : ""}`}
                                  title={[
                                    course ? `${course.code} — ${course.title}` : pc.courseId,
                                    info ? ta("plans.hasPrereqs", { count: info.count, codes: info.codes }) : null,
                                    missingTitle,
                                  ].filter(Boolean).join("\n")}
                                  draggable={!readOnly}
                                  onDragStart={(e) => {
                                    if (readOnly) return;
                                    e.dataTransfer.effectAllowed = "move";
                                    setDragPc(pc);
                                  }}
                                  onDragEnd={() => { setDragPc(null); setDragOver(null); }}
                                >
                                  <span className="aplans-grid-course-code">{course?.code || "—"}</span>
                                  {info && (
                                    <span
                                      className={`aplans-grid-prereq-chip${info.missing.length ? " missing" : ""}`}
                                      aria-label={ta("plans.hasPrereqs", { count: info.count, codes: info.codes })}
                                    >
                                      {info.missing.length ? <AlertTriangle size={9} /> : null}
                                      {info.count}
                                    </span>
                                  )}
                                  <button
                                    type="button"
                                    className={`aplans-grid-course-tag ${pc.isMandatory ? "mandatory" : "elective"}`}
                                    title={readOnly ? undefined : t("toggle_mandatory")}
                                    disabled={readOnly}
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      if (!readOnly) onUpdateCourse?.(pc, { isMandatory: !pc.isMandatory });
                                    }}
                                  >
                                    {pc.isMandatory ? "R" : "E"}
                                  </button>
                                  {!readOnly && (
                                    <button
                                      type="button"
                                      className="aplans-grid-course-remove"
                                      onClick={(e) => { e.stopPropagation(); onRemoveCourse?.(pc); }}
                                      title={t("remove")}
                                    >
                                      <X size={10} />
                                    </button>
                                  )}
                                </div>
                              );
                            })}
                            <span className={`aplans-grid-cell-credits ${creditTone(credits)}`}>
                              {ta("plans.semesterTotal", { credits })}
                            </span>
                          </div>
                        ) : (
                          <div className="aplans-grid-empty-cell">—</div>
                        )}
                        {!readOnly && (
                          <button
                            type="button"
                            className="aplans-grid-add-btn"
                            onClick={(e) => {
                              e.stopPropagation();
                              setTarget(isTarget ? null : { level, semester });
                            }}
                            title={t("add_course_here")}
                            aria-label={`${t("add_course")} — ${t("level")} ${level}, ${t("semester")} ${semester}`}
                          >
                            <Plus size={12} />
                          </button>
                        )}
                      </td>
                    );
                  })}
                </tr>
              );
            })}
          </tbody>
          <tfoot>
            <tr>
              <td className="aplans-grid-corner">∑</td>
              {columnTotals.map((total, i) => (
                <td key={i} className="aplans-grid-col-total">
                  {total > 0 ? ta("plans.semesterTotal", { credits: total }) : "—"}
                </td>
              ))}
            </tr>
          </tfoot>
        </table>
      </div>

      {!readOnly && target && (
        <AddCoursePanel
          courseCatalog={courseCatalog}
          planCourses={planCourses}
          initialLevel={target.level}
          initialSemester={target.semester}
          onAdd={handleAdd}
          onClose={() => setTarget(null)}
        />
      )}

      <CreditSummaryBar planCourses={planCourses} courseMap={courseMap} />

      {(!planCourses || planCourses.length === 0) && (
        <div className="aplans-curriculum-empty">
          <BookOpen size={32} />
          <p>{t("grid_empty_hint")}</p>
        </div>
      )}
    </div>
  );
}

export default CurriculumGrid;
