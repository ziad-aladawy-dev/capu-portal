import { useMemo } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  ArrowLeft, ClipboardCheck, ScrollText, BookMarked,
  Award, Layers, AlertTriangle, ServerCog, CheckCircle2, Clock,
} from "lucide-react";
import userService from "../../users/services/userService";
import DataTable from "../../../core/components/DataTable";
import StatusBadge from "../../../core/components/StatusBadge";
import { SkeletonCard, SkeletonStats } from "../../../core/components/Skeleton";
import { usePlanByStructure } from "../../../core/query/useAcademicPlans";
import { useActiveCourses } from "../../../core/query/useCourses";
import {
  useStudentTranscript, useStudentGradeSummary, useStudentRegistered,
} from "../../../core/query/useStudentAcademics";
import { REGISTRATION_STATUS_LABELS } from "../../../core/services/studentAcademicsService";
import "../styles/academicHub.css";

const RESULT_STATUS = { 0: "In Progress", 1: "Passed", 2: "Failed", 3: "Withdrawn", 4: "Incomplete" };

function gradeColor(status) {
  // by AcademicResultStatus
  if (status === 1) return { background: "#dcfce7", color: "#166534" };
  if (status === 2) return { background: "#fee2e2", color: "#b91c1c" };
  if (status === 0) return { background: "#dbeafe", color: "#1d4ed8" };
  return { background: "#f3f4f6", color: "#6b7280" };
}

function isPendingEndpoint(query) {
  return query.isError && (query.error?.response?.status === 404 || query.error?.response?.status === 501 || !query.error?.response);
}

export default function StudentAcademicHubPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const studentQ = useQuery({
    queryKey: ["student", id],
    queryFn: () => userService.getStudentById(id),
    enabled: !!id,
  });
  const student = studentQ.data;

  const planQ = usePlanByStructure(student?.structureNodeId);
  const transcriptQ = useStudentTranscript(id);
  const summaryQ = useStudentGradeSummary(id);
  const registeredQ = useStudentRegistered(id);
  const { data: courses = [] } = useActiveCourses();

  const courseById = useMemo(() => {
    const m = {};
    courses.forEach((c) => (m[c.id] = c));
    return m;
  }, [courses]);

  const transcript = transcriptQ.data;
  const summary = transcript?.summary || summaryQ.data;

  // Sets used by the plan audit.
  const { passedSet, inProgressSet } = useMemo(() => {
    const passed = new Set();
    const inProg = new Set();
    if (transcript?.categories) {
      for (const cat of transcript.categories) {
        for (const c of [...(cat.compulsory || []), ...(cat.elective || [])]) {
          if (c.status === 1) passed.add(c.courseId);
          else if (c.status === 0) inProg.add(c.courseId);
        }
      }
    }
    for (const r of registeredQ.data || []) {
      if (r.status === 0) inProg.add(r.courseId); // Enrolled
    }
    return { passedSet: passed, inProgressSet: inProg };
  }, [transcript, registeredQ.data]);

  const plan = planQ.data;
  const planAudit = useMemo(() => {
    const pcs = plan?.planCourses || [];
    const total = pcs.length;
    const done = pcs.filter((p) => passedSet.has(p.courseId)).length;
    const progress = pcs.filter((p) => !passedSet.has(p.courseId) && inProgressSet.has(p.courseId)).length;
    const pct = total > 0 ? Math.round((done / total) * 100) : 0;
    // group by level
    const byLevel = {};
    for (const p of pcs) {
      (byLevel[p.level] ||= []).push(p);
    }
    return { total, done, progress, pct, byLevel };
  }, [plan, passedSet, inProgressSet]);

  if (studentQ.isLoading) {
    return <div className="ah-page"><SkeletonStats count={4} /><div style={{ height: 16 }} /><SkeletonCard height={320} /></div>;
  }
  if (studentQ.isError || !student) {
    return (
      <div className="ah-page">
        <div style={{ display: "flex", alignItems: "center", gap: 8, color: "#b91c1c" }}>
          <AlertTriangle size={18} /> Student not found.
        </div>
        <button className="btn-cancel" style={{ marginTop: 12 }} onClick={() => navigate("/admin/students")}>
          <ArrowLeft size={14} /> Back to directory
        </button>
      </div>
    );
  }

  const initial = (student.localizedName || student.name || "S").charAt(0);
  const academicPending = isPendingEndpoint(transcriptQ) && isPendingEndpoint(summaryQ);

  return (
    <div className="ah-page">
      <div className="ah-topbar">
        <button className="btn-cancel" onClick={() => navigate(`/admin/students/${id}`)} style={{ padding: "6px 10px" }}>
          <ArrowLeft size={14} /> Profile
        </button>
        <div className="ah-student">
          <div className="ah-avatar">{initial}</div>
          <div>
            <h1>{student.localizedName || student.name}</h1>
            <p>
              {student.studentCode} · {student.programName || student.structureNodeName || "—"}
              {student.levelName ? ` · ${student.levelName}` : ""}
            </p>
          </div>
        </div>
        <div style={{ marginLeft: "auto" }}>
          <StatusBadge status={student.isActive ? "active" : "inactive"} />
        </div>
      </div>

      {/* Summary cards */}
      {summaryQ.isLoading && !summary ? (
        <SkeletonStats count={4} />
      ) : summary ? (
        <div className="ah-summary">
          <div className="ah-card accent">
            <div className="v">{Number(summary.cgpa ?? 0).toFixed(2)}</div>
            <div className="l">CGPA</div>
          </div>
          <div className="ah-card"><div className="v">{Number(summary.gpa ?? 0).toFixed(2)}</div><div className="l">Term GPA</div></div>
          <div className="ah-card"><div className="v">{summary.earnedCredits ?? 0}</div><div className="l">Credits Earned</div></div>
          <div className="ah-card"><div className="v">{summary.remainingCredits ?? "—"}</div><div className="l">Credits Left</div></div>
          <div className="ah-card">
            <div className="v" style={{ fontSize: 15 }}>{summary.academicStanding || "—"}</div>
            <div className="l">Standing</div>
          </div>
        </div>
      ) : null}

      <div className="ah-grid">
        {/* LEFT: Plan audit */}
        <div className="ah-panel">
          <div className="ah-panel-head">
            <h2><ClipboardCheck size={15} /> Degree Audit</h2>
            {plan && <span style={{ fontSize: 11, color: "#6b7280" }}>{plan.name}</span>}
          </div>

          {planQ.isLoading ? (
            <SkeletonCard height={200} />
          ) : !student.structureNodeId ? (
            <div className="ah-pending"><Layers size={28} style={{ color: "#c9a84c" }} /><p style={{ margin: 0, fontSize: 13, color: "#6b7280" }}>Student has no structure node assigned.</p></div>
          ) : planQ.isError || !plan ? (
            <div className="ah-pending">
              <Layers size={28} style={{ color: "#c9a84c" }} />
              <p style={{ margin: 0, fontSize: 13, color: "#6b7280" }}>No academic plan found for this program.</p>
            </div>
          ) : planAudit.total === 0 ? (
            <div className="ah-pending">
              <Layers size={28} style={{ color: "#c9a84c" }} />
              <p style={{ margin: 0, fontSize: 13, color: "#374151", fontWeight: 600 }}>{plan.name}</p>
              <p style={{ margin: 0, fontSize: 12, color: "#6b7280" }}>This plan has no courses assigned yet — add them in Academic Plans.</p>
            </div>
          ) : (
            <>
              <div className="ah-progress-wrap">
                <div className="ah-progress-pct">{planAudit.pct}%</div>
                <div style={{ flex: 1 }}>
                  <div className="ah-progress-bar">
                    <div className="ah-progress-fill" style={{ width: `${planAudit.pct}%` }} />
                  </div>
                  <div style={{ display: "flex", gap: 14, marginTop: 8, fontSize: 11, color: "#6b7280" }}>
                    <span style={{ display: "inline-flex", alignItems: "center", gap: 4 }}><CheckCircle2 size={12} style={{ color: "#22c55e" }} /> {planAudit.done} done</span>
                    <span style={{ display: "inline-flex", alignItems: "center", gap: 4 }}><Clock size={12} style={{ color: "#3b82f6" }} /> {planAudit.progress} in progress</span>
                    <span>of {planAudit.total} courses</span>
                  </div>
                </div>
              </div>

              {academicPending && (
                <div style={{ fontSize: 11, color: "#92400e", background: "#fffbeb", border: "1px solid #fde68a", borderRadius: 6, padding: "6px 10px", marginBottom: 10 }}>
                  Completion data is unavailable until the admin transcript endpoint is live — showing plan structure only.
                </div>
              )}

              {Object.keys(planAudit.byLevel).sort((a, b) => a - b).map((lvl) => (
                <div key={lvl} className="ah-level">
                  <div className="ah-level-title">Level {lvl}</div>
                  {planAudit.byLevel[lvl]
                    .sort((a, b) => a.semester - b.semester)
                    .map((p) => {
                      const c = courseById[p.courseId];
                      const done = passedSet.has(p.courseId);
                      const prog = !done && inProgressSet.has(p.courseId);
                      const cls = done ? "done" : prog ? "progress" : "";
                      return (
                        <div key={p.id} className={`ah-course-pill ${cls}`}>
                          <span className={`ah-dot ${done ? "done" : prog ? "progress" : "none"}`} />
                          <span className="code">{c?.code || String(p.courseId).slice(0, 6)}</span>
                          <span className="title">{c?.title || "Course"}</span>
                          <span className={`ah-req-tag ${p.isMandatory ? "" : "elective"}`}>
                            {p.isMandatory ? "REQ" : "ELEC"}
                          </span>
                          <span style={{ fontSize: 11, color: "#9ca3af" }}>S{p.semester}</span>
                        </div>
                      );
                    })}
                </div>
              ))}
            </>
          )}
        </div>

        {/* RIGHT: Transcript timeline + enrollments */}
        <div style={{ display: "flex", flexDirection: "column", gap: 18 }}>
          <div className="ah-panel">
            <div className="ah-panel-head">
              <h2><ScrollText size={15} /> Transcript</h2>
            </div>
            {transcriptQ.isLoading ? (
              <SkeletonCard height={200} />
            ) : isPendingEndpoint(transcriptQ) ? (
              <PendingEndpoint route={`GET /api/students/${id}/transcript`} />
            ) : !transcript?.categories?.length ? (
              <div className="ah-pending"><ScrollText size={28} style={{ color: "#c9a84c" }} /><p style={{ margin: 0, fontSize: 13, color: "#6b7280" }}>No transcript records yet.</p></div>
            ) : (
              transcript.categories.map((cat) => (
                <div key={cat.category} style={{ marginBottom: 14 }}>
                  <div className="ah-level-title" style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <Award size={12} /> {cat.displayName}
                  </div>
                  {[...(cat.compulsory || []), ...(cat.elective || [])].map((c) => {
                    const col = gradeColor(c.status);
                    return (
                      <div key={c.courseId + (c.grade || "")} className="ah-grade-row">
                        <span className="code">{c.courseCode}</span>
                        <span className="title">{c.courseTitle}</span>
                        <span style={{ fontSize: 11, color: "#9ca3af" }}>{c.creditHours}cr</span>
                        <span className="ah-grade-badge" style={col} title={RESULT_STATUS[c.status]}>{c.grade || "—"}</span>
                      </div>
                    );
                  })}
                </div>
              ))
            )}
          </div>

          <div className="ah-panel">
            <div className="ah-panel-head">
              <h2><BookMarked size={15} /> Current Enrollments</h2>
            </div>
            {registeredQ.isLoading ? (
              <SkeletonCard height={140} />
            ) : isPendingEndpoint(registeredQ) ? (
              <PendingEndpoint route={`GET /api/students/${id}/registered`} />
            ) : (registeredQ.data || []).length === 0 ? (
              <div className="ah-pending"><BookMarked size={28} style={{ color: "#c9a84c" }} /><p style={{ margin: 0, fontSize: 13, color: "#6b7280" }}>No active registrations.</p></div>
            ) : (
              <DataTable
                columns={[
                  { key: "courseCode", label: "Code", width: 90, render: (v) => <span style={{ fontFamily: "Space Mono, monospace", fontWeight: 700, color: "#1a1f5e" }}>{v}</span> },
                  { key: "courseTitle", label: "Course", nowrap: false },
                  { key: "creditHours", label: "Cr", align: "center", width: 48 },
                  { key: "semesterName", label: "Semester", width: 120 },
                  {
                    key: "status", label: "Status", width: 110,
                    render: (v) => <StatusBadge status={v === 1 ? "active" : v === 0 ? "open" : v === 3 ? "cancelled" : "inactive"} label={REGISTRATION_STATUS_LABELS[v] || "—"} />,
                  },
                ]}
                data={registeredQ.data}
                rowKey="id"
                compact
              />
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function PendingEndpoint({ route }) {
  return (
    <div className="ah-pending">
      <ServerCog size={28} style={{ color: "#c9a84c" }} />
      <p style={{ margin: 0, fontSize: 13, color: "#374151", fontWeight: 600 }}>Admin endpoint pending</p>
      <p style={{ margin: 0, fontSize: 12, color: "#6b7280" }}>
        This panel activates once <code>{route}</code> is deployed.
      </p>
    </div>
  );
}
