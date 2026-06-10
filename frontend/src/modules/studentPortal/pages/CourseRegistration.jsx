import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Search, BookOpen, Info, AlertCircle } from "lucide-react";
import * as courseService from "../../../core/services/courseService";
import "../styles/studentCourses.css";

/**
 * Read-only course catalog (browse + search). Per the documented Registration
 * model the portal does NOT register students — actual enrollment happens in the
 * external academic system — so this page is a catalog reference only.
 */
function CourseCatalog() {
  const { t } = useTranslation();
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");

  useEffect(() => {
    const id = setTimeout(() => setDebounced(term.trim()), 300);
    return () => clearTimeout(id);
  }, [term]);

  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ["course-catalog", debounced],
    staleTime: 60_000,
    queryFn: async () => {
      const res = await courseService.searchCourses(debounced ? { search: debounced, pageSize: 30 } : { pageSize: 30 });
      const items = Array.isArray(res) ? res : res?.items || [];
      return items;
    },
  });

  const courses = data || [];

  return (
    <div className="student-courses-container">
      <div className="sc-header">
        <h1>{t("courses.catalog", { defaultValue: "Course Catalog" })}</h1>
        <p>{t("courses.catalog_subtitle", { defaultValue: "Browse the course catalog. Registration is handled by the registrar." })}</p>
      </div>

      <div className="cat-search">
        <Search size={16} />
        <input
          type="text"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          placeholder={t("courses.search_placeholder", { defaultValue: "Search by code, title, or keyword…" })}
        />
      </div>

      <div className="cat-note">
        <Info size={14} />
        {t("courses.readonly_note", { defaultValue: "This catalog is read-only. To register or drop a course, contact the registrar / your academic system." })}
      </div>

      {isLoading ? (
        <div className="sc-status"><div className="sc-spinner" /></div>
      ) : isError ? (
        <div className="sc-status">
          <AlertCircle size={40} color="#dc2626" />
          <p className="sc-status-text">Unable to load the catalog.</p>
          <button className="sc-retry-btn" onClick={() => refetch()}>Try Again</button>
        </div>
      ) : courses.length === 0 ? (
        <div className="sc-status">
          <BookOpen size={40} />
          <h3>No courses found</h3>
          <p className="sc-status-text">{debounced ? `No matches for "${debounced}".` : "No courses in the catalog."}</p>
        </div>
      ) : (
        <div className="sc-section">
          <h2>{t("courses.results", { defaultValue: "Results" })} ({courses.length})</h2>
          <div className="sc-courses-grid">
            {courses.map((c) => (
              <div key={c.id} className="sc-course-card active">
                <div className="card-header">
                  <h3>{c.title || c.name}</h3>
                  <span className="course-code">{c.code}</span>
                </div>
                <div className="card-info">
                  <div className="info-row"><span className="label">Credits:</span><span>{c.creditHours ?? "—"}</span></div>
                  {c.category != null && (
                    <div className="info-row">
                      <span className="label">Category:</span>
                      <span>{courseService.getCourseCategoryLabel(c.category)}</span>
                    </div>
                  )}
                </div>
                {c.description && <p className="cat-desc">{c.description}</p>}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export default CourseCatalog;
