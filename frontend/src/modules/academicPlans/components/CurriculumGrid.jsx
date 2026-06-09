import { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { BookOpen, Plus, Trash2, X, Search } from "lucide-react";

const MAX_LEVEL = 10;
const MAX_SEMESTER = 4;

function CurriculumGrid({
  planCourses,
  courseCatalog,
  onAddCourse,
  onRemoveCourse,
  onBatchUpdate,
}) {
  const { t } = useTranslation();
  const [showAddPanel, setShowAddPanel] = useState(null);
  const [courseSearch, setCourseSearch] = useState("");

  const grid = useMemo(() => {
    const g = {};
    for (let l = 1; l <= MAX_LEVEL; l++) {
      for (let s = 1; s <= MAX_SEMESTER; s++) {
        g[`${l}-${s}`] = [];
      }
    }
    if (planCourses) {
      for (const pc of planCourses) {
        const key = `${pc.level}-${pc.semester}`;
        if (g[key]) g[key].push(pc);
      }
    }
    return g;
  }, [planCourses]);

  const courseMap = useMemo(() => {
    const m = {};
    if (courseCatalog) {
      for (const c of courseCatalog) m[c.id] = c;
    }
    return m;
  }, [courseCatalog]);

  const filteredCatalog = useMemo(() => {
    if (!courseCatalog) return [];
    if (!courseSearch.trim()) return courseCatalog;
    const q = courseSearch.toLowerCase();
    return courseCatalog.filter(
      (c) =>
        c.code?.toLowerCase().includes(q) ||
        c.title?.toLowerCase().includes(q)
    );
  }, [courseCatalog, courseSearch]);

  const alreadyInPlan = useMemo(() => {
    const ids = new Set();
    if (planCourses) {
      for (const pc of planCourses) ids.add(pc.courseId);
    }
    return ids;
  }, [planCourses]);

  function isInCell(courseId, level, semester) {
    return (planCourses || []).some(
      (pc) => pc.courseId === courseId && pc.level === level && pc.semester === semester
    );
  }

  function handleRemove(pc) {
    if (onRemoveCourse) onRemoveCourse(pc);
  }

  function handleAddToCell(courseId, level, semester) {
    if (onAddCourse) {
      onAddCourse({
        courseId,
        level,
        semester,
        isMandatory: true,
      });
    }
    setShowAddPanel(null);
  }

  function isLevelEmpty(level) {
    for (let s = 1; s <= MAX_SEMESTER; s++) {
      if (grid[`${level}-${s}`].length > 0) return false;
    }
    return true;
  }

  return (
    <div className="aplans-curriculum">
      <div className="aplans-curriculum-grid-wrap">
        <table className="aplans-grid-table">
          <thead>
            <tr>
              <th className="aplans-grid-corner">Level</th>
              {Array.from({ length: MAX_SEMESTER }, (_, i) => (
                <th key={i} className="aplans-grid-sem-header">
                  Semester {i + 1}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {Array.from({ length: MAX_LEVEL }, (_, li) => {
              const level = li + 1;
              return (
                <tr
                  key={level}
                  className={`aplans-grid-row ${isLevelEmpty(level) ? "empty-level" : ""}`}
                >
                  <td className="aplans-grid-level-cell">
                    <span className="aplans-grid-level-num">{level}</span>
                  </td>
                  {Array.from({ length: MAX_SEMESTER }, (_, si) => {
                    const semester = si + 1;
                    const key = `${level}-${semester}`;
                    const courses = grid[key] || [];
                    return (
                      <td
                        key={semester}
                        className={`aplans-grid-cell ${courses.length === 0 ? "empty" : ""}`}
                      >
                        {courses.length > 0 ? (
                          <div className="aplans-grid-cell-courses">
                            {courses.map((pc) => {
                              const course = courseMap[pc.courseId];
                              return (
                                <div
                                  key={pc.id}
                                  className={`aplans-grid-course-badge ${pc.isMandatory ? "mandatory" : "elective"}`}
                                  title={
                                    course
                                      ? `${course.code} — ${course.title}`
                                      : pc.courseId
                                  }
                                >
                                  <span className="aplans-grid-course-code">
                                    {course?.code || "—"}
                                  </span>
                                  {pc.isMandatory ? (
                                    <span className="aplans-grid-course-mandatory" title="Mandatory">R</span>
                                  ) : (
                                    <span className="aplans-grid-course-elective" title="Elective">E</span>
                                  )}
                                  <button
                                    className="aplans-grid-course-remove"
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      handleRemove(pc);
                                    }}
                                    title="Remove from plan"
                                  >
                                    <X size={10} />
                                  </button>
                                </div>
                              );
                            })}
                          </div>
                        ) : (
                          <div className="aplans-grid-empty-cell">—</div>
                        )}
                        <button
                          className="aplans-grid-add-btn"
                          onClick={(e) => {
                            e.stopPropagation();
                            setShowAddPanel(
                              showAddPanel?.level === level &&
                                showAddPanel?.semester === semester
                                ? null
                                : { level, semester }
                            );
                          }}
                          title="Add course here"
                        >
                          <Plus size={12} />
                        </button>
                        {showAddPanel &&
                          showAddPanel.level === level &&
                          showAddPanel.semester === semester && (
                            <div
                              className="aplans-grid-quick-add"
                              onClick={(e) => e.stopPropagation()}
                            >
                              <div className="aplans-quick-add-header">
                                <Search size={11} />
                                <input
                                  type="text"
                                  placeholder="Search courses..."
                                  value={courseSearch}
                                  onChange={(e) => setCourseSearch(e.target.value)}
                                  autoFocus
                                />
                                <button
                                  className="aplans-quick-add-close"
                                  onClick={() => {
                                    setShowAddPanel(null);
                                    setCourseSearch("");
                                  }}
                                >
                                  <X size={12} />
                                </button>
                              </div>
                              <div className="aplans-quick-add-list">
                                {filteredCatalog.length === 0 ? (
                                  <div className="aplans-quick-add-empty">
                                    No courses match
                                  </div>
                                ) : (
                                  filteredCatalog.map((c) => {
                                    const inPlan = alreadyInPlan.has(c.id);
                                    const alreadyHere = isInCell(
                                      c.id,
                                      level,
                                      semester
                                    );
                                    return (
                                      <button
                                        key={c.id}
                                        className={`aplans-quick-add-item ${alreadyHere ? "exists" : ""}`}
                                        disabled={inPlan && !alreadyHere}
                                        onClick={() =>
                                          !alreadyHere &&
                                          handleAddToCell(c.id, level, semester)
                                        }
                                        title={
                                          inPlan && !alreadyHere
                                            ? "Already in a different cell — remove first"
                                            : alreadyHere
                                              ? "Already placed here"
                                              : `Add ${c.code}`
                                        }
                                      >
                                        <span className="aplans-quick-add-code">
                                          {c.code}
                                        </span>
                                        <span className="aplans-quick-add-title">
                                          {c.title}
                                        </span>
                                        <span className="aplans-quick-add-credits">
                                          {c.creditHours} cr
                                        </span>
                                      </button>
                                    );
                                  })
                                )}
                              </div>
                            </div>
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

      {!planCourses || planCourses.length === 0 ? (
        <div className="aplans-curriculum-empty">
          <BookOpen size={32} />
          <p>No courses in this plan. Click <strong>+</strong> on any cell to add one.</p>
        </div>
      ) : null}
    </div>
  );
}

export default CurriculumGrid;
