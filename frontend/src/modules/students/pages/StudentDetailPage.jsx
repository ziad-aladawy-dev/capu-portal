import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router-dom";
import {
  User, Mail, Phone, Calendar, Hash, BookOpen, GraduationCap,
  Receipt, FileText, ClipboardList, Flag, AlertCircle, CheckCircle,
  XCircle, Edit3, Key, Trash2, ArrowLeft, Clock
} from "lucide-react";
import userService from "../../users/services/userService";
import { useToast } from "../../../core/components/Toast";
import { useStickySelection } from "../../../core/contexts/StickySelectionContext";
import LoadingSpinner from "../../users/components/LoadingSpinner";
import ErrorMessage from "../../users/components/ErrorMessage";
import "../../users/styles/userDetails.css";
import "../../users/styles/userForms.css";
import "../../users/styles/users.css";

const TABS = [
  { id: "overview", label: "Overview", icon: User },
  { id: "personal", label: "Personal Info", icon: FileText },
  { id: "academic", label: "Academic History", icon: GraduationCap },
  { id: "enrollments", label: "Enrollments", icon: BookOpen },
  { id: "payments", label: "Payments", icon: Receipt },
  { id: "documents", label: "Documents", icon: FileText },
  { id: "activity", label: "Activity Log", icon: ClipboardList },
  { id: "notes", label: "Notes & Flags", icon: Flag },
];

function StudentDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const { addToast } = useToast();
  const { select } = useStickySelection();
  const [student, setStudent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [activeTab, setActiveTab] = useState("overview");

  const loadStudent = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await userService.getStudentById(id);
      setStudent(data);
      select({ id, name: data.name, code: data.studentCode, type: "student" });
    } catch (err) {
      setError(err.message || "Student not found");
    } finally {
      setLoading(false);
    }
  }, [id, select]);

  useEffect(() => { loadStudent(); }, [loadStudent]);

  const handleToggleActive = async () => {
    if (!student) return;
    try {
      await userService.toggleStudentStatus(id);
      setStudent(prev => ({ ...prev, isActive: !prev.isActive }));
      addToast(`Student ${student.isActive ? "deactivated" : "activated"}`, "success");
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  const handleDelete = async () => {
    if (!window.confirm("Permanently delete this student? This cannot be undone.")) return;
    try {
      await userService.deleteStudent(id);
      addToast("Student deleted", "success");
      navigate("/admin/students");
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  if (loading) return <LoadingSpinner fullPage message="Loading student details..." />;
  if (error || !student) return <ErrorMessage message={error || "Student not found"} />;

  const s = student;
  const isExpired = s.passwordStatus === "Expired";

  return (
    <div className="user-details-layout">
      <div className="page-content">
        <div className="left-column animated-fade">
          <div className="profile-card">
            <div className="avatar-shell">
              <div className="avatar-main">{s.name?.charAt(0) || "S"}</div>
            </div>
            <h1 className="profile-name">{s.name}</h1>
            <p className="profile-email">{s.email}</p>
            <div className="profile-badges">
              <span className="role-badge">Student</span>
              <span className={`status-badge ${s.isActive ? "status-active" : "status-inactive"}`}>
                <span className="status-dot" />{s.isActive ? "Active" : "Inactive"}
              </span>
              <span className={`password-badge ${isExpired ? "password-expired" : "password-valid"}`}>
                {isExpired ? "Password Expired" : "Valid Password"}
              </span>
            </div>
            <div className="mini-stats">
              <div className="mini-stat">
                <span>Student Code</span>
                <strong style={{ fontFamily: "Space Mono, monospace" }}>{s.studentCode || "—"}</strong>
              </div>
              <div className="mini-stat">
                <span>Member Since</span>
                <strong>{s.createdAt ? new Date(s.createdAt).toLocaleDateString("en-US", { year: "numeric", month: "short" }) : "—"}</strong>
              </div>
            </div>
          </div>
        </div>

        <div className="right-column animated-fade delay-2">
          <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
            <button className="bottom-action-btn soft-gold" onClick={() => navigate("/admin/students")} style={{ padding: "6px 10px" }}>
              <ArrowLeft size={14} /> Back
            </button>
          </div>

          <div className="tabs-container">
            <div className="tabs-row">
              {TABS.map(tab => {
                const Icon = tab.icon;
                return (
                  <button key={tab.id} className={`tab-item ${activeTab === tab.id ? "active" : ""}`} onClick={() => setActiveTab(tab.id)}>
                    <Icon size={13} /> {tab.label}
                  </button>
                );
              })}
            </div>
          </div>

          <div className="tab-content-card tab-switch-animate">
            {activeTab === "overview" && <OverviewTab student={s} />}
            {activeTab === "personal" && <PersonalInfoTab student={s} onRefresh={loadStudent} />}
            {activeTab === "academic" && <AcademicHistoryTab student={s} />}
            {activeTab === "enrollments" && <EnrollmentsTab studentId={id} />}
            {activeTab === "payments" && <PaymentsTab studentId={id} />}
            {activeTab === "documents" && <DocumentsTab studentId={id} />}
            {activeTab === "activity" && <ActivityTab studentId={id} />}
            {activeTab === "notes" && <NotesTab studentId={id} />}
          </div>

          <div className="bottom-actions-panel animated-fade">
            <div className="bottom-actions-row">
              <button className="bottom-action-btn gold" onClick={() => navigate(`/admin/users/edit-student/${id}`)}>
                <Edit3 size={18} /> Edit Student
              </button>
              <button className="bottom-action-btn soft-gold" onClick={() => addToast("Password reset not yet available", "info")}>
                <Key size={18} /> Reset Password
              </button>
              <button className="bottom-action-btn soft-gold" onClick={() => navigate(`/admin/students/${id}/profile-records`)}>
                <FileText size={18} /> Profile Records
              </button>
              <button className="bottom-action-btn gold" onClick={() => navigate(`/admin/students/${id}/academics`)}>
                <GraduationCap size={18} /> Academic Hub
              </button>
              <span className="action-separator" />
              <button className={`bottom-action-btn ${s.isActive ? "soft-red" : "soft-green"}`} onClick={handleToggleActive}>
                {s.isActive ? <XCircle size={18} /> : <CheckCircle size={18} />}
                {s.isActive ? "Deactivate" : "Activate"}
              </button>
              <button className="bottom-action-btn soft-red" onClick={handleDelete}>
                <Trash2 size={18} /> Delete
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function OverviewTab({ student }) {
  const s = student;
  return (
    <div>
      <h2 className="section-title">Overview</h2>
      <div className="details-grid">
        <div className="detail-card">
          <div className="detail-icon"><Hash size={15} /></div>
          <div className="detail-content">
            <div className="detail-label">Student Code</div>
            <div className="detail-value" style={{ fontFamily: "Space Mono, monospace" }}>{s.studentCode || "—"}</div>
          </div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Mail size={15} /></div>
          <div className="detail-content">
            <div className="detail-label">Email</div>
            <div className="detail-value">{s.email || "—"}</div>
          </div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Calendar size={15} /></div>
          <div className="detail-content">
            <div className="detail-label">Date of Birth</div>
            <div className="detail-value">{s.birthDate ? new Date(s.birthDate).toLocaleDateString() : "—"}</div>
          </div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Clock size={15} /></div>
          <div className="detail-content">
            <div className="detail-label">Member Since</div>
            <div className="detail-value">{s.createdAt ? new Date(s.createdAt).toLocaleDateString() : "—"}</div>
          </div>
        </div>
        <div className="detail-card full">
          <div className="detail-icon"><AlertCircle size={15} /></div>
          <div className="detail-content">
            <div className="detail-label">Status</div>
            <div className="detail-value">
              <span className={`status-badge ${s.isActive ? "status-active" : "status-inactive"}`}>
                <span className="status-dot" />{s.isActive ? "Active" : "Inactive"}
              </span>
              <span className={`password-badge ${s.passwordStatus === "Expired" ? "password-expired" : "password-valid"}`} style={{ marginLeft: 8 }}>
                {s.passwordStatus === "Expired" ? "Password Expired" : "Valid Password"}
              </span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function PersonalInfoTab({ student, onRefresh }) {
  const s = student;
  const [editing, setEditing] = useState(false);
  const { addToast } = useToast();
  const [form, setForm] = useState({
    name: s.name || "",
    nationalId: s.nationalId || "",
    email: s.email || "",
    phoneNumber: s.phoneNumber || "",
    birthDate: s.birthDate ? s.birthDate.split("T")[0] : "",
  });

  const handleSave = async () => {
    try {
      await userService.updateStudent(s.id, form);
      addToast("Student info updated", "success");
      setEditing(false);
      onRefresh();
    } catch (err) {
      addToast(err.message, "error");
    }
  };

  if (editing) {
    return (
      <div>
        <h2 className="section-title">Personal Information</h2>
        <div className="details-grid">
          {[
            { label: "Full Name", name: "name", icon: User, value: form.name },
            { label: "National ID", name: "nationalId", icon: Hash, value: form.nationalId },
            { label: "Email", name: "email", icon: Mail, value: form.email },
            { label: "Phone", name: "phoneNumber", icon: Phone, value: form.phoneNumber },
            { label: "Date of Birth", name: "birthDate", icon: Calendar, value: form.birthDate, type: "date" },
          ].map(field => (
            <div className="form-group" key={field.name} style={{ padding: "0 0 12px" }}>
              <label className="form-label">{field.label}</label>
              <div className="input-wrapper">
                <input
                  type={field.type || "text"}
                  value={form[field.name]}
                  onChange={e => setForm(prev => ({ ...prev, [field.name]: e.target.value }))}
                  className="form-input"
                />
                <field.icon size={15} className="input-icon" />
              </div>
            </div>
          ))}
        </div>
        <div style={{ display: "flex", gap: 8, marginTop: 12 }}>
          <button className="wizard-btn primary" onClick={handleSave}>Save Changes</button>
          <button className="wizard-btn secondary" onClick={() => setEditing(false)}>Cancel</button>
        </div>
      </div>
    );
  }

  return (
    <div>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
        <h2 className="section-title" style={{ margin: 0 }}>Personal Information</h2>
        <button className="bottom-action-btn gold" onClick={() => setEditing(true)}><Edit3 size={14} /> Edit</button>
      </div>
      <div className="details-grid">
        <div className="detail-card">
          <div className="detail-icon"><User size={15} /></div>
          <div className="detail-content"><div className="detail-label">Full Name</div><div className="detail-value">{s.name}</div></div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Hash size={15} /></div>
          <div className="detail-content"><div className="detail-label">National ID</div><div className="detail-value" style={{ fontFamily: "Space Mono, monospace" }}>{s.nationalId}</div></div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Mail size={15} /></div>
          <div className="detail-content"><div className="detail-label">Email</div><div className="detail-value">{s.email}</div></div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Phone size={15} /></div>
          <div className="detail-content"><div className="detail-label">Phone</div><div className="detail-value">{s.phoneNumber || "—"}</div></div>
        </div>
        <div className="detail-card">
          <div className="detail-icon"><Calendar size={15} /></div>
          <div className="detail-content"><div className="detail-label">Date of Birth</div><div className="detail-value">{s.birthDate ? new Date(s.birthDate).toLocaleDateString() : "—"}</div></div>
        </div>
      </div>
    </div>
  );
}

function AcademicHistoryTab({ student }) {
  return (
    <div>
      <h2 className="section-title">Academic History</h2>
      <div className="empty-state" style={{ padding: "40px 18px" }}>
        <GraduationCap size={40} style={{ color: "#c9a84c", marginBottom: 12 }} />
        <h3 style={{ color: "#1a1f5e", margin: "0 0 6px", fontSize: 14 }}>Academic Records</h3>
        <p style={{ color: "#6b7280", fontSize: 12, margin: 0 }}>Academic history will appear here once enrollments are configured.</p>
      </div>
    </div>
  );
}

function EnrollmentsTab({ studentId }) {
  return (
    <div>
      <h2 className="section-title">Current Enrollments</h2>
      <div className="empty-state" style={{ padding: "40px 18px" }}>
        <BookOpen size={40} style={{ color: "#c9a84c", marginBottom: 12 }} />
        <h3 style={{ color: "#1a1f5e", margin: "0 0 6px", fontSize: 14 }}>No Enrollments Yet</h3>
        <p style={{ color: "#6b7280", fontSize: 12, margin: 0 }}>Course enrollments will appear once the student registers for courses.</p>
      </div>
    </div>
  );
}

function PaymentsTab({ studentId }) {
  const navigate = useNavigate();
  const [fees, setFees] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const treasuryService = await import("../../../core/services/treasuryService");
        const data = await treasuryService.fetchUnpaidFees(studentId);
        if (!cancelled) setFees(Array.isArray(data) ? data : []);
      } catch {
        // viewer may not hold the payments permission
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [studentId]);

  if (loading) return <div><h2 className="section-title">Payments</h2><p style={{ color: "#6b7280", fontSize: 12 }}>Loading...</p></div>;

  const outstanding = fees.reduce((s, f) => s + Number(f.totalAmount ?? 0), 0);
  const currency = fees[0]?.currency || "EGP";

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
        <h2 className="section-title">Outstanding Fees</h2>
        <button
          className="btn-primary"
          style={{ padding: "7px 14px", fontSize: 12 }}
          onClick={() => navigate(`/admin/finance/treasury?studentId=${studentId}`)}
        >
          <Receipt size={13} /> Collect in Treasury
        </button>
      </div>
      {fees.length === 0 ? (
        <div className="empty-state" style={{ padding: "40px 18px" }}>
          <CheckCircle size={40} style={{ color: "#16a34a", marginBottom: 12 }} />
          <h3 style={{ color: "#1a1f5e", margin: "0 0 6px", fontSize: 14 }}>No Outstanding Fees</h3>
          <p style={{ color: "#6b7280", fontSize: 12, margin: 0 }}>This student has no unpaid Treasury fees.</p>
        </div>
      ) : (
        <>
          <p style={{ fontSize: 12, color: "#6b7280", margin: "6px 0 12px" }}>
            {fees.length} unpaid fee(s) · total{" "}
            <strong style={{ color: "#b91c1c" }}>
              {outstanding.toLocaleString("en-US", { minimumFractionDigits: 2 })} {currency}
            </strong>
          </p>
          <table className="users-table" style={{ minWidth: 500 }}>
            <thead>
              <tr>
                <th>Source</th>
                <th>Date</th>
                <th>Qty</th>
                <th>Amount</th>
              </tr>
            </thead>
            <tbody>
              {fees.map(fee => (
                <tr key={fee.id}>
                  <td>{fee.sourceModule || "—"}</td>
                  <td>{fee.createdAt ? new Date(fee.createdAt).toLocaleDateString() : "—"}</td>
                  <td>{fee.quantity}</td>
                  <td>{Number(fee.totalAmount).toLocaleString("en-US", { minimumFractionDigits: 2 })} {fee.currency}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  );
}

function DocumentsTab({ studentId }) {
  return (
    <div>
      <h2 className="section-title">Documents</h2>
      <div className="empty-state" style={{ padding: "40px 18px" }}>
        <FileText size={40} style={{ color: "#c9a84c", marginBottom: 12 }} />
        <h3 style={{ color: "#1a1f5e", margin: "0 0 6px", fontSize: 14 }}>No Documents</h3>
        <p style={{ color: "#6b7280", fontSize: 12, margin: 0 }}>Uploaded documents (transcripts, IDs, photos) will appear here.</p>
      </div>
    </div>
  );
}

function ActivityTab({ studentId }) {
  return (
    <div>
      <h2 className="section-title">Activity Log</h2>
      <div className="empty-state" style={{ padding: "40px 18px" }}>
        <ClipboardList size={40} style={{ color: "#c9a84c", marginBottom: 12 }} />
        <h3 style={{ color: "#1a1f5e", margin: "0 0 6px", fontSize: 14 }}>No Activity Yet</h3>
        <p style={{ color: "#6b7280", fontSize: 12, margin: 0 }}>All changes to this student's record will be logged here.</p>
      </div>
    </div>
  );
}

function NotesTab({ studentId }) {
  return (
    <div>
      <h2 className="section-title">Notes & Flags</h2>
      <div className="empty-state" style={{ padding: "40px 18px" }}>
        <Flag size={40} style={{ color: "#c9a84c", marginBottom: 12 }} />
        <h3 style={{ color: "#1a1f5e", margin: "0 0 6px", fontSize: 14 }}>No Notes</h3>
        <p style={{ color: "#6b7280", fontSize: 12, margin: 0 }}>Internal staff notes and behavioral flags will appear here.</p>
      </div>
    </div>
  );
}

export default StudentDetailPage;
