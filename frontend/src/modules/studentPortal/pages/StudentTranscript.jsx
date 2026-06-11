import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  GraduationCap, Download, FileText, AlertCircle, ChevronDown, ChevronRight,
  History, Loader2, BadgeCheck, Printer,
} from "lucide-react";
import { useTranscript, useRegistrationHistory, useCourseAttempts } from "../hooks/useAcademics";
import { GRADE_POINTS, TRANSCRIPT_CATEGORY, downloadTranscriptPdf } from "../../../core/services/gradeService";
import { getRegistrationStatusLabel, REGISTRATION_STATUS } from "../../../core/services/registrationService";
import "../styles/studentTranscript.css";

function gradeColor(grade) {
  const g = GRADE_POINTS[grade];
  if (g == null) return "";
  if (g >= 3.7) return "grade-a";
  if (g >= 3.0) return "grade-b";
  if (g >= 2.0) return "grade-c";
  return "grade-d";
}

const CATEGORY_ORDER = [
  TRANSCRIPT_CATEGORY.General,
  TRANSCRIPT_CATEGORY.Faculty,
  TRANSCRIPT_CATEGORY.MainSpecialization,
];

function StudentTranscript() {
  const { t } = useTranslation();
  const [tab, setTab] = useState("transcript");

  return (
    <div className="tr-container">
      <div className="tr-header">
        <h1>{t("transcript.title", { defaultValue: "Transcript & Course History" })}</h1>
        <p>{t("transcript.subtitle", { defaultValue: "Your official academic record, synced from academic systems" })}</p>
      </div>

      <div className="tr-tabs">
        <button className={`tr-tab ${tab === "transcript" ? "active" : ""}`} onClick={() => setTab("transcript")}>
          <FileText size={16} /> {t("transcript.tab_transcript", { defaultValue: "Transcript" })}
        </button>
        <button className={`tr-tab ${tab === "history" ? "active" : ""}`} onClick={() => setTab("history")}>
          <History size={16} /> {t("transcript.tab_history", { defaultValue: "Course History" })}
        </button>
      </div>

      {tab === "transcript" ? <TranscriptTab /> : <CourseHistoryTab />}
    </div>
  );
}

// ── Transcript tab ───────────────────────────────────────────────────────────
function TranscriptTab() {
  const { t } = useTranslation();
  const { data: transcript, isLoading, isError } = useTranscript();
  const [downloading, setDownloading] = useState(false);
  const [downloadError, setDownloadError] = useState(null);

  async function handleDownload() {
    setDownloadError(null);
    setDownloading(true);
    try {
      await downloadTranscriptPdf();
    } catch {
      setDownloadError(t("transcript.pdf_error", { defaultValue: "Couldn't generate the PDF. Please try again." }));
    } finally {
      setDownloading(false);
    }
  }

  if (isLoading) {
    return <div className="tr-section tr-muted">{t("common.loading", { defaultValue: "Loading…" })}</div>;
  }
  if (isError) {
    return (
      <div className="tr-section tr-error">
        <AlertCircle size={18} /> {t("transcript.load_error", { defaultValue: "Couldn't load your transcript." })}
      </div>
    );
  }
  if (!transcript) {
    return (
      <div className="tr-section tr-empty">
        <GraduationCap size={32} />
        <p>{t("transcript.empty", { defaultValue: "No transcript records have been synced yet." })}</p>
      </div>
    );
  }

  const summary = transcript.summary;

  return (
    <>
      <div className="tr-identity">
        <div>
          <div className="tr-student-name">{transcript.studentName || "—"}</div>
          <div className="tr-student-code">{transcript.studentCode}</div>
        </div>
        <div className="tr-identity-actions">
          <button className="tr-pdf-btn tr-print-btn" onClick={() => window.print()} type="button">
            <Printer size={16} />
            {t("transcript.print", { defaultValue: "Print" })}
          </button>
          <button className="tr-pdf-btn" onClick={handleDownload} disabled={downloading}>
            {downloading ? <Loader2 size={16} className="tr-spin" /> : <Download size={16} />}
            {downloading
              ? t("transcript.generating", { defaultValue: "Generating…" })
              : t("transcript.download_pdf", { defaultValue: "Download Official PDF" })}
          </button>
        </div>
      </div>
      {downloadError && <div className="tr-section tr-error"><AlertCircle size={16} /> {downloadError}</div>}

      {summary && (
        <div className="tr-summary-grid">
          <SummaryCard label={t("grades.cgpa", { defaultValue: "Cumulative GPA" })} value={Number(summary.cgpa).toFixed(2)} highlight />
          <SummaryCard label={t("transcript.earned_credits", { defaultValue: "Credits Earned" })} value={summary.earnedCredits} />
          <SummaryCard label={t("transcript.remaining_credits", { defaultValue: "Credits Remaining" })} value={summary.remainingCredits} />
          <SummaryCard label={t("grades.standing", { defaultValue: "Academic Standing" })} value={summary.academicStanding || "—"} />
        </div>
      )}

      <div className="tr-categories">
        {CATEGORY_ORDER.map((cat) => {
          const c = (transcript.categories || []).find((x) => x.category === cat);
          if (!c) return null;
          return <CategoryBlock key={cat} category={c} />;
        })}
      </div>
    </>
  );
}

function SummaryCard({ label, value, highlight }) {
  return (
    <div className={`tr-summary-card ${highlight ? "highlight" : ""}`}>
      <div className="tr-summary-value">{value}</div>
      <div className="tr-summary-label">{label}</div>
    </div>
  );
}

function CategoryBlock({ category }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(true);

  const compulsory = category.compulsory || [];
  const elective = category.elective || [];
  const all = [...compulsory, ...elective];
  const earned = all.reduce((s, c) => s + (c.creditsEarned || 0), 0);
  const totalCredits = all.reduce((s, c) => s + (c.creditHours || 0), 0);

  return (
    <div className="tr-category">
      <button className="tr-category-head" onClick={() => setOpen((o) => !o)}>
        {open ? <ChevronDown size={18} /> : <ChevronRight size={18} />}
        <span className="tr-category-name">{category.displayName}</span>
        <span className="tr-category-meta">
          {all.length} {t("transcript.courses", { defaultValue: "courses" })} · {earned}/{totalCredits} {t("transcript.cr", { defaultValue: "cr" })}
        </span>
      </button>
      {open && (
        <div className="tr-category-body">
          {compulsory.length > 0 && (
            <CourseTable title={t("transcript.compulsory", { defaultValue: "Compulsory" })} courses={compulsory} />
          )}
          {elective.length > 0 && (
            <CourseTable title={t("transcript.elective", { defaultValue: "Elective" })} courses={elective} />
          )}
          {all.length === 0 && <p className="tr-muted tr-pad">{t("transcript.no_courses", { defaultValue: "No courses in this category." })}</p>}
        </div>
      )}
    </div>
  );
}

function CourseTable({ title, courses }) {
  const { t } = useTranslation();
  return (
    <div className="tr-course-group">
      <h4 className="tr-course-group-title">{title}</h4>
      <table className="tr-table">
        <thead>
          <tr>
            <th>{t("transcript.code", { defaultValue: "Code" })}</th>
            <th>{t("transcript.course", { defaultValue: "Course" })}</th>
            <th>{t("transcript.cr", { defaultValue: "Cr" })}</th>
            <th>{t("transcript.grade", { defaultValue: "Grade" })}</th>
            <th>{t("transcript.score", { defaultValue: "Score" })}</th>
            <th>{t("transcript.earned", { defaultValue: "Earned" })}</th>
          </tr>
        </thead>
        <tbody>
          {courses.map((c) => (
            <tr key={c.courseId}>
              <td>{c.courseCode}</td>
              <td className="tr-course-title">{c.courseTitle}</td>
              <td>{c.creditHours}</td>
              <td><span className={`tr-grade-badge ${gradeColor(c.grade)}`}>{c.grade || "-"}</span></td>
              <td>{c.numericScore != null ? Number(c.numericScore).toFixed(0) : "-"}</td>
              <td>{c.creditsEarned}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

// ── Course history tab ───────────────────────────────────────────────────────
function CourseHistoryTab() {
  const { t } = useTranslation();
  const { data: history = [], isLoading, isError } = useRegistrationHistory();

  if (isLoading) {
    return <div className="tr-section tr-muted">{t("common.loading", { defaultValue: "Loading…" })}</div>;
  }
  if (isError) {
    return (
      <div className="tr-section tr-error">
        <AlertCircle size={18} /> {t("transcript.history_error", { defaultValue: "Couldn't load your course history." })}
      </div>
    );
  }
  if (history.length === 0) {
    return (
      <div className="tr-section tr-empty">
        <History size={32} />
        <p>{t("transcript.history_empty", { defaultValue: "No registration history recorded yet." })}</p>
      </div>
    );
  }

  return (
    <div className="tr-history">
      {history.map((sem) => (
        <div key={sem.semesterId} className="tr-history-semester">
          <h3 className="tr-history-sem-name">{sem.semesterName}</h3>
          <table className="tr-table">
            <thead>
              <tr>
                <th></th>
                <th>{t("transcript.code", { defaultValue: "Code" })}</th>
                <th>{t("transcript.course", { defaultValue: "Course" })}</th>
                <th>{t("transcript.cr", { defaultValue: "Cr" })}</th>
                <th>{t("transcript.attempt", { defaultValue: "Attempt" })}</th>
                <th>{t("transcript.status", { defaultValue: "Status" })}</th>
              </tr>
            </thead>
            <tbody>
              {(sem.courses || []).map((c) => (
                <HistoryRow key={c.id} course={c} />
              ))}
            </tbody>
          </table>
        </div>
      ))}
    </div>
  );
}

function HistoryRow({ course }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const isRepeat = course.attemptNumber > 1;
  // Only fetch the full attempt history when the row is expanded.
  const { data: attempts = [], isLoading } = useCourseAttempts(open ? course.courseId : null);

  return (
    <>
      <tr className="tr-history-row" onClick={() => setOpen((o) => !o)}>
        <td className="tr-expand-cell">{open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}</td>
        <td>{course.courseCode}</td>
        <td className="tr-course-title">
          {course.courseTitle}
          {isRepeat && <span className="tr-repeat-tag">{t("transcript.retake", { defaultValue: "Retake" })} #{course.attemptNumber}</span>}
        </td>
        <td>{course.creditHours}</td>
        <td>{course.attemptNumber}</td>
        <td><StatusBadge status={course.status} /></td>
      </tr>
      {open && (
        <tr className="tr-attempts-row">
          <td colSpan={6}>
            {isLoading ? (
              <span className="tr-muted"><Loader2 size={14} className="tr-spin" /> {t("common.loading", { defaultValue: "Loading…" })}</span>
            ) : attempts.length <= 1 ? (
              <span className="tr-muted">{t("transcript.single_attempt", { defaultValue: "No repeat history — taken once." })}</span>
            ) : (
              <div className="tr-attempts">
                <span className="tr-attempts-title"><BadgeCheck size={14} /> {t("transcript.all_attempts", { defaultValue: "All attempts" })}:</span>
                {attempts.map((a) => (
                  <div key={a.id} className="tr-attempt-item">
                    <span>#{a.attemptNumber}</span>
                    <span>{a.semesterName}</span>
                    <StatusBadge status={a.status} />
                  </div>
                ))}
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  );
}

function StatusBadge({ status }) {
  const cls =
    status === REGISTRATION_STATUS.Completed ? "ok"
      : status === REGISTRATION_STATUS.Enrolled ? "active"
        : status === REGISTRATION_STATUS.Withdrawn ? "warn"
          : status === REGISTRATION_STATUS.Failed ? "bad"
            : "neutral";
  return <span className={`tr-status-badge ${cls}`}>{getRegistrationStatusLabel(status)}</span>;
}

export default StudentTranscript;
