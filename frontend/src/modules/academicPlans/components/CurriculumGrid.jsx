import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { BookOpen, Plus, X } from "lucide-react";
import AddCoursePanel from "./AddCoursePanel";
import CreditSummaryBar from "./CreditSummaryBar";

const MAX_LEVEL = 10;
const MAX_SEMESTER = 4;

/**
 * Visual curriculum matrix (levels × semesters). Supports the full CRUD surface:
 *   - Create: "+" on any cell opens the shared AddCoursePanel pre-targeted to it
 *   - Read:   course badges per cell, mandatory/elective colour-coded
 *   - Update: click the R/E pill to flip mandatory; drag a badge to move it
 *   - Delete: the × on a badge
 *
 * Only the matrix scrolls horizontally; the add panel renders below it so its
 * list is never clipped.
 */
function CurriculumGrid({
  planCourses,
  courseCatalog,
  onAddCourse,
  onRemoveCourse,
  onUpdateCourse,
  readOnly = false,
}) {
  const { t } = useTranslation();
  const [target, setTarget] = useState(null); // { level, semester } cell being added to
  const [dragPc, setDragPc] = useState(null);
  const [dragOver, setDragOver] = useState(null); // "level-semester"

  const courseMap = useMemo(() => {
    const m = {};
    for (const c of courseCatalog || []) m[c.id] = c;
    return m;
  }, [courseCatalog]);

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
                              return (
                                <div
                                  key={pc.id}
                                  className={`aplans-grid-course-badge ${pc.isMandatory ? "mandatory" : "elective"} ${dragPc?.id === pc.id ? "dragging" : ""}`}
                                  title={course ? `${course.code} — ${course.title}` : pc.courseId}
                                  draggable={!readOnly}
                                  onDragStart={(e) => {
                                    if (readOnly) return;
                                    e.dataTransfer.effectAllowed = "move";
                                    setDragPc(pc);
                                  }}
                                  onDragEnd={() => { setDragPc(null); setDragOver(null); }}
                                >
                                  <span className="aplans-grid-course-code">{course?.code || "—"}</span>
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
