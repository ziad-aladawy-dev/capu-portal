import { useEffect, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Search, History, UserRound, ChevronRight } from "lucide-react";
import * as studentService from "../../../core/services/studentService";
import { readRecentStudents, rememberStudent } from "../utils/recentStudents";

/**
 * Typeahead student search used by the cashier workspace. Searches after two
 * characters with a 300 ms debounce, and offers the employee's recently
 * served students for one-click reselection.
 */
export default function StudentPicker({ onSelect }) {
  const { t } = useTranslation();
  const [term, setTerm] = useState("");
  const [debounced, setDebounced] = useState("");

  useEffect(() => {
    const timer = setTimeout(() => setDebounced(term.trim()), 300);
    return () => clearTimeout(timer);
  }, [term]);

  const enabled = debounced.length >= 2;

  const { data, isFetching, error } = useQuery({
    queryKey: ["students", "search", { Search: debounced, scope: "treasury-picker" }],
    queryFn: () =>
      studentService.searchStudents({ Search: debounced, Page: 1, PageSize: 8 }),
    enabled,
    staleTime: 30 * 1000,
  });

  const results = useMemo(() => {
    const items = data?.items;
    return Array.isArray(items) ? items : [];
  }, [data]);

  const recents = useMemo(() => readRecentStudents(), []);

  const pick = (student) => {
    rememberStudent(student);
    onSelect(student);
  };

  return (
    <div className="ty-picker">
      <div className="ty-picker-search">
        <Search size={16} aria-hidden="true" />
        <input
          autoFocus
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          placeholder={t("treasury_student_search_placeholder")}
          aria-label={t("treasury_student_search_placeholder")}
        />
      </div>

      {!enabled && recents.length > 0 && (
        <div className="ty-picker-recents">
          <div className="ty-picker-section-label">
            <History size={13} aria-hidden="true" />
            {t("treasury_recent_students")}
          </div>
          {recents.map((s) => (
            <StudentRow key={s.id} student={s} onClick={() => pick(s)} />
          ))}
        </div>
      )}

      {enabled && (
        <div className="ty-picker-results" role="listbox" aria-busy={isFetching}>
          {error && (
            <div className="ty-picker-hint ty-picker-error">
              {t("treasury_student_search_failed")}
            </div>
          )}
          {!error && isFetching && results.length === 0 && (
            <div className="ty-picker-hint">{t("searching")}…</div>
          )}
          {!error && !isFetching && results.length === 0 && (
            <div className="ty-picker-hint">{t("treasury_no_students_found")}</div>
          )}
          {results.map((s) => (
            <StudentRow key={s.id} student={s} onClick={() => pick(s)} />
          ))}
        </div>
      )}
    </div>
  );
}

function StudentRow({ student, onClick }) {
  return (
    <button type="button" className="ty-picker-row" onClick={onClick}>
      <span className="ty-picker-avatar" aria-hidden="true">
        <UserRound size={15} />
      </span>
      <span className="ty-picker-main">
        <span className="ty-picker-name">{student.name}</span>
        <span className="ty-picker-sub">
          {student.studentCode && <span className="ty-code-pill">{student.studentCode}</span>}
          {student.email && <span className="ty-picker-email">{student.email}</span>}
        </span>
      </span>
      <ChevronRight size={15} className="ty-picker-chevron" aria-hidden="true" />
    </button>
  );
}
