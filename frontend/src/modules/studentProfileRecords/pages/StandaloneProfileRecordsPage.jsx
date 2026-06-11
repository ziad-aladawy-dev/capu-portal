import { useEffect, useState, useRef } from "react";
import { useTranslation } from "react-i18next";
import { FileText, X, Search, UserCheck } from "lucide-react";
import * as studentService from "../../../core/services/studentService";
import { useStickySelection } from "../../../core/contexts/StickySelectionContext";
import { getLocalized } from "../../../core/utils/getLocalized";
import ProfileRecordsPanel from "../components/ProfileRecordsPanel";
import PageHeader from "../../../core/components/PageHeader";
import "../styles/studentProfileRecords.css";
import "../styles/standaloneProfileRecords.css";

// Management wrapper: /admin/student-information/profile-records
// (student picker + the shared records panel)
function StandaloneProfileRecordsPage() {
  const { t, i18n } = useTranslation();
  const searchRef = useRef(null);
  const debounceRef = useRef(null);

  const [searchQuery, setSearchQuery] = useState("");
  const [studentResults, setStudentResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [showDropdown, setShowDropdown] = useState(false);

  // The directory-sidebar pin IS the selection: a pinned student shows here
  // however the page was reached, and picking one locally re-pins it, so the
  // page and the sidebar never disagree. Clearing in either place clears both.
  const { selected: pinnedUser, select: pinUser, clear: clearPin } = useStickySelection();
  const selectedStudent = pinnedUser?.type === "student" ? pinnedUser : null;

  useEffect(() => {
    searchRef.current?.focus();
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, []);

  const handleSearchInput = (value) => {
    setSearchQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (!value.trim()) {
      setStudentResults([]);
      setShowDropdown(false);
      setSearching(false);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const result = await studentService.searchStudents({ search: value, page: 1, pageSize: 8 });
        const items = result?.items || [];
        setStudentResults(items);
        setShowDropdown(items.length > 0);
      } catch {
        setStudentResults([]);
      } finally {
        setSearching(false);
      }
    }, 300);
  };

  const selectStudent = (student) => {
    pinUser({
      id: student.id,
      name: getLocalized(student.name || student.fullNameEn, i18n.language),
      code: student.studentCode || "",
      type: "student",
    });
    setShowDropdown(false);
    setSearchQuery("");
    setStudentResults([]);
  };

  const clearStudent = () => {
    clearPin();
    setSearchQuery("");
    setStudentResults([]);
    setShowDropdown(false);
  };

  return (
    <div className="spr-page">
      <PageHeader
        icon={FileText}
        title={t("profile_records")}
        subtitle={t("manage_profile_records")}
      />

      <div className="sprs-search-section">
        <div className="sprs-search-input-wrap">
          <Search size={16} className="sprs-search-icon" />
          <input
            ref={searchRef}
            type="text"
            className="sprs-search-input"
            placeholder={t("search_student_placeholder")}
            value={searchQuery}
            onChange={(e) => handleSearchInput(e.target.value)}
            onFocus={() => {
              if (studentResults.length > 0) setShowDropdown(true);
            }}
            onBlur={() => setTimeout(() => setShowDropdown(false), 200)}
          />
          {searchQuery && (
            <button className="sprs-search-clear" onClick={() => handleSearchInput("")}><X size={14} /></button>
          )}
          {searching && <div className="sprs-search-spinner" />}
        </div>

        {showDropdown && studentResults.length > 0 && (
          <div className="sprs-dropdown">
            {studentResults.map((s) => (
              <button
                key={s.id}
                className="sprs-dropdown-item"
                onClick={() => selectStudent(s)}
              >
                <UserCheck size={15} />
                <div className="sprs-dropdown-item-text">
                  <span className="sprs-dropdown-item-name">{s.name || s.fullNameEn}</span>
                  <span className="sprs-dropdown-item-code">{s.studentCode || s.email || "—"}</span>
                </div>
              </button>
            ))}
          </div>
        )}
      </div>

      {selectedStudent ? (
        <>
          <div className="sprs-selected-student">
            <FileText size={15} />
            <span>
              {t("viewing_records_for")} <strong>{selectedStudent.name}</strong>
              {selectedStudent.code && <> · {selectedStudent.code}</>}
            </span>
            <button className="sprs-selected-clear" onClick={clearStudent}>
              <X size={13} /> {t("change_student")}
            </button>
          </div>
          <ProfileRecordsPanel studentId={selectedStudent.id} />
        </>
      ) : (
        <div className="sprs-select-hint">
          <Search size={32} />
          <h3>{t("select_student")}</h3>
          <p>{t("select_student_hint")}</p>
        </div>
      )}
    </div>
  );
}

export default StandaloneProfileRecordsPage;
