import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  User, Mail, Phone, Calendar, Hash, BookOpen, GraduationCap,
  Receipt, FileText, ClipboardCheck, AlertCircle, CheckCircle,
  XCircle, Edit3, Key, Trash2, ArrowLeft, Clock, Wallet,
  Building2, Layers, Users as UsersIcon, ShieldAlert, CreditCard,
  BadgeCheck, CircleDashed, ExternalLink, Landmark,
} from "lucide-react";
import { useToast } from "../../../core/components/Toast";
import { useStickySelection } from "../../../core/contexts/StickySelectionContext";
import { useStudent, useUpdateStudent, useToggleStudentStatus, useDeleteStudent, studentKey } from "../../../core/query/useStudents";
import { SkeletonCard, SkeletonStats } from "../../../core/components/Skeleton";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import ErrorMessage from "../../../core/components/ErrorMessage";
import { getLocalized, parseLocalizedValue } from "../../../core/utils/getLocalized";
import userService from "../../users/services/userService";
import * as treasuryService from "../../../core/services/treasuryService";
import { fetchProfileRecords, getProfileCategoryLabel } from "../../../core/services/studentProfileService";
import {
  ProfileHero, StatCard, TabBar, Panel, Field, EmptyState,
  ResetPasswordModal, CopyButton,
} from "../../users/components/ProfileKit";
import {
  fmtDate, fmtDateTime, fmtMoney, yearsSince, daysUntil, resolvePhotoUrl,
} from "../../users/components/profileUtils";

const TABS = (feeCount, recordCount) => [
  { id: "overview", label: "Overview", icon: User },
  { id: "finance", label: "Finance", icon: Wallet, count: feeCount },
  { id: "records", label: "Records", icon: ClipboardCheck, count: recordCount },
  { id: "account", label: "Account", icon: ShieldAlert },
];

/**
 * The update endpoint replaces the whole entity (and requires a valid Level
 * structure node), so every partial edit must echo the current values back.
 */
function buildUpdatePayload(s, overrides = {}) {
  const { ar, en } = parseLocalizedValue(s.name);
  return {
    nameAr: ar || "",
    nameEn: en || "",
    nationalId: s.nationalId || "",
    birthDate: s.birthDate,
    phoneNumber: s.phoneNumber || "",
    email: s.email || "",
    photoUrl: s.photoUrl ?? null,
    gender: s.gender ?? null,
    guardianName: s.guardianName ?? null,
    guardianPhone: s.guardianPhone ?? null,
    structureNodeId: s.structureNodeId,
    isActive: s.isActive,
    ...overrides,
  };
}

function StudentDetailPage() {
  const { id } = useParams();
  // Remount per student so tab selection and tab-local state never leak
  // from one profile into another.
  return <StudentDetailContent key={id} id={id} />;
}

function StudentDetailContent({ id }) {
  const navigate = useNavigate();
  const { i18n } = useTranslation();
  const { addToast } = useToast();
  const { select } = useStickySelection();
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState("overview");
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);

  const studentQuery = useStudent(id);
  const student = studentQuery.data;
  const updateStudent = useUpdateStudent();
  const toggleStatus = useToggleStudentStatus();
  const deleteStudent = useDeleteStudent();

  // Live side-data. Each tolerates failure (viewer may lack the permission).
  const feesQuery = useQuery({
    queryKey: ["student-unpaid-fees", id],
    queryFn: () => treasuryService.fetchUnpaidFees(id),
    retry: false,
    enabled: !!id,
  });
  const ordersQuery = useQuery({
    queryKey: ["student-orders", id],
    queryFn: () => treasuryService.fetchOrdersForStudent(id),
    retry: false,
    enabled: !!id,
  });
  const recordsQuery = useQuery({
    queryKey: ["student-profile-records", id],
    queryFn: () => fetchProfileRecords(id),
    retry: false,
    enabled: !!id,
  });

  const fees = Array.isArray(feesQuery.data) ? feesQuery.data : [];
  const orders = Array.isArray(ordersQuery.data) ? ordersQuery.data : [];
  const records = Array.isArray(recordsQuery.data) ? recordsQuery.data : [];
  const outstanding = fees.reduce((sum, f) => sum + Number(f.totalAmount ?? 0), 0);
  const currency = fees[0]?.currency || "EGP";
  const verifiedRecords = records.filter((r) => r.verifiedAt).length;

  const uploadPhoto = useMutation({
    mutationFn: (file) => userService.uploadStudentPhoto(id, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: studentKey(id) });
      addToast("Photo updated", "success");
    },
    onError: (err) => addToast(err.message, "error"),
  });

  // Keep the directory sidebar pin in sync with the freshest student data —
  // including renames, which update the ["student", id] cache via mutations.
  const localizedName = student ? getLocalized(student.name, i18n.language) : "";
  useEffect(() => {
    if (student?.id) {
      select({ id: student.id, name: localizedName, code: student.studentCode, type: "student" });
    }
  }, [student?.id, localizedName, student?.studentCode, select]);

  const handleToggleActive = () => {
    if (!student || toggleStatus.isPending) return;
    toggleStatus.mutate(id, {
      onSuccess: () => addToast(`Student ${student.isActive ? "deactivated" : "activated"}`, "success"),
      onError: (err) => addToast(err.message, "error"),
    });
  };

  const handleDelete = () => {
    deleteStudent.mutate(id, {
      onSuccess: () => {
        addToast("Student deleted", "success");
        navigate("/admin/students");
      },
      onError: (err) => {
        setDeleteOpen(false);
        addToast(err.message, "error");
      },
    });
  };

  const handleResetPassword = (password) =>
    new Promise((resolve) => {
      updateStudent.mutate(
        { id, ...buildUpdatePayload(student, { password, confirmPassword: password }) },
        {
          onSuccess: () => {
            addToast("Password has been reset", "success");
            setResetOpen(false);
            resolve();
          },
          onError: (err) => {
            addToast(err.response?.data?.message || err.message, "error");
            resolve();
          },
        },
      );
    });

  if (studentQuery.isPending) {
    return (
      <div className="pp-page">
        <SkeletonCard height={140} />
        <div style={{ height: 14 }} />
        <SkeletonStats count={4} />
        <div style={{ height: 14 }} />
        <SkeletonCard height={300} />
      </div>
    );
  }
  if (studentQuery.isError || !student) {
    return <ErrorMessage message={studentQuery.error?.message || "Student not found"} />;
  }

  const s = student;
  const isExpired = s.passwordStatus === "Expired";
  const expiryDays = daysUntil(s.passwordExpiry);
  const age = yearsSince(s.birthDate);

  return (
    <div className="pp-page">
      <div className="pp-topbar">
        <button className="pp-back" onClick={() => navigate("/admin/students")}>
          <ArrowLeft size={13} /> Student Directory
        </button>
      </div>

      <ProfileHero
        photoUrl={resolvePhotoUrl(s.photoUrl)}
        initial={(localizedName || "S").charAt(0).toUpperCase()}
        name={localizedName}
        subtitle={
          <>
            <span style={{ fontFamily: "Space Mono, monospace" }}>{s.studentCode}</span>
            <span>·</span>
            <span>{s.email}</span>
            <CopyButton value={s.email} label="Email" />
          </>
        }
        badges={
          <>
            <span className="pp-badge tone-student"><GraduationCap size={10} /> Student</span>
            <span className={`pp-badge ${s.isActive ? "tone-good" : "tone-bad"}`}>
              <span className="pp-badge-dot" /> {s.isActive ? "Active" : "Inactive"}
            </span>
            {isExpired && <span className="pp-badge tone-bad"><Key size={10} /> Password Expired</span>}
          </>
        }
        chips={
          <>
            {s.facultyName && <span className="pp-chip"><Building2 size={11} /> {s.facultyName}</span>}
            {s.programName && (
              <>
                <span className="pp-chip-sep">›</span>
                <span className="pp-chip"><BookOpen size={11} /> {s.programName}</span>
              </>
            )}
            {s.levelName && (
              <>
                <span className="pp-chip-sep">›</span>
                <span className="pp-chip"><Layers size={11} /> {s.levelName}</span>
              </>
            )}
          </>
        }
        actions={
          <>
            <button className="pp-hero-btn primary" onClick={() => navigate(`/admin/users/students/${id}/edit`)}>
              <Edit3 size={13} /> Edit Student
            </button>
            <button className="pp-hero-btn ghost" onClick={() => navigate(`/admin/students/${id}/academics`)}>
              <GraduationCap size={13} /> Academic Hub
            </button>
            <button className="pp-hero-btn ghost" onClick={() => navigate(`/admin/finance/treasury?studentId=${id}`)}>
              <Wallet size={13} /> Treasury
            </button>
          </>
        }
        onUploadPhoto={(file) => uploadPhoto.mutate(file)}
        uploading={uploadPhoto.isPending}
      />

      <div className="pp-stats pp-fade">
        <StatCard
          icon={Receipt}
          label="Outstanding Fees"
          value={feesQuery.isPending ? "…" : fmtMoney(outstanding, currency)}
          hint={fees.length ? `${fees.length} unpaid fee${fees.length > 1 ? "s" : ""}` : "Nothing due"}
          tone={outstanding > 0 ? "danger" : "good"}
          onClick={() => setActiveTab("finance")}
        />
        <StatCard
          icon={BadgeCheck}
          label="Profile Records"
          value={recordsQuery.isPending ? "…" : `${verifiedRecords}/${records.length} verified`}
          hint={records.length ? "Click to review" : "No records yet"}
          tone="gold"
          onClick={() => setActiveTab("records")}
        />
        <StatCard
          icon={Key}
          label="Password"
          value={isExpired ? "Expired" : expiryDays !== null ? `${expiryDays} days left` : "Valid"}
          hint={s.passwordExpiry ? `Expires ${fmtDate(s.passwordExpiry)}` : "No expiry set"}
          tone={isExpired ? "danger" : ""}
          onClick={() => setActiveTab("account")}
        />
        <StatCard
          icon={Clock}
          label="Member Since"
          value={fmtDate(s.createdAt)}
          hint={age !== null ? `Student is ${age} years old` : undefined}
        />
      </div>

      <TabBar
        tabs={TABS(fees.length || null, records.length || null)}
        active={activeTab}
        onChange={setActiveTab}
      />

      <div className="pp-fade" key={activeTab}>
        {activeTab === "overview" && (
          <OverviewTab student={s} updateStudent={updateStudent} addToast={addToast} />
        )}
        {activeTab === "finance" && (
          <FinanceTab
            studentId={id}
            fees={fees}
            orders={orders}
            feesQuery={feesQuery}
            ordersQuery={ordersQuery}
            navigate={navigate}
          />
        )}
        {activeTab === "records" && (
          <RecordsTab studentId={id} records={records} query={recordsQuery} navigate={navigate} />
        )}
        {activeTab === "account" && (
          <AccountTab
            student={s}
            onToggleActive={handleToggleActive}
            togglePending={toggleStatus.isPending}
            onResetPassword={() => setResetOpen(true)}
            onDelete={() => setDeleteOpen(true)}
          />
        )}
      </div>

      <ResetPasswordModal
        open={resetOpen}
        onClose={() => setResetOpen(false)}
        userName={localizedName}
        onSubmit={handleResetPassword}
        pending={updateStudent.isPending}
      />

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => { if (!deleteStudent.isPending) setDeleteOpen(false); }}
        onConfirm={handleDelete}
        title="Delete Student"
        message={`Permanently delete ${localizedName}?`}
        detail="This cannot be undone."
        confirmLabel="Delete"
        variant="danger"
        loading={deleteStudent.isPending}
      />
    </div>
  );
}

/* ── Overview ───────────────────────────────────────────────── */

function OverviewTab({ student: s, updateStudent, addToast }) {
  const [editing, setEditing] = useState(false);
  const [form, setForm] = useState(null);

  // Snapshot the latest values when edit starts so reopening after a save
  // never shows stale data.
  const startEdit = () => {
    const { ar, en } = parseLocalizedValue(s.name);
    setForm({
      nameAr: ar || "",
      nameEn: en || "",
      email: s.email || "",
      phoneNumber: s.phoneNumber || "",
      birthDate: s.birthDate ? s.birthDate.split("T")[0] : "",
      gender: s.gender || "",
      guardianName: s.guardianName || "",
      guardianPhone: s.guardianPhone || "",
    });
    setEditing(true);
  };

  const save = () => {
    if (updateStudent.isPending) return;
    updateStudent.mutate(
      {
        id: s.id,
        ...buildUpdatePayload(s, {
          nameAr: form.nameAr,
          nameEn: form.nameEn,
          email: form.email,
          phoneNumber: form.phoneNumber,
          birthDate: form.birthDate || s.birthDate,
          gender: form.gender || null,
          guardianName: form.guardianName || null,
          guardianPhone: form.guardianPhone || null,
        }),
      },
      {
        onSuccess: () => {
          addToast("Student info updated", "success");
          setEditing(false);
        },
        onError: (err) => addToast(err.response?.data?.message || err.message, "error"),
      },
    );
  };

  if (editing && form) {
    const fields = [
      { label: "Name (Arabic)", name: "nameAr" },
      { label: "Name (English)", name: "nameEn" },
      { label: "Email", name: "email", type: "email" },
      { label: "Phone", name: "phoneNumber" },
      { label: "Date of Birth", name: "birthDate", type: "date" },
      { label: "Guardian Name", name: "guardianName" },
      { label: "Guardian Phone", name: "guardianPhone" },
    ];
    return (
      <Panel icon={Edit3} title="Quick Edit">
        <div className="pp-form-grid">
          {fields.map((f) => (
            <div className="pp-form-group" key={f.name}>
              <label className="pp-form-label">{f.label}</label>
              <input
                type={f.type || "text"}
                className="pp-input"
                value={form[f.name]}
                onChange={(e) => setForm((prev) => ({ ...prev, [f.name]: e.target.value }))}
              />
            </div>
          ))}
          <div className="pp-form-group">
            <label className="pp-form-label">Gender</label>
            <select
              className="pp-select"
              value={form.gender}
              onChange={(e) => setForm((prev) => ({ ...prev, gender: e.target.value }))}
            >
              <option value="">Not specified</option>
              <option value="Male">Male</option>
              <option value="Female">Female</option>
            </select>
          </div>
        </div>
        <div className="pp-form-actions">
          <button className="pp-btn navy" onClick={save} disabled={updateStudent.isPending}>
            {updateStudent.isPending ? "Saving…" : "Save Changes"}
          </button>
          <button className="pp-btn soft" onClick={() => setEditing(false)} disabled={updateStudent.isPending}>
            Cancel
          </button>
        </div>
      </Panel>
    );
  }

  const { ar: nameAr, en: nameEn } = parseLocalizedValue(s.name);

  return (
    <>
      <Panel
        icon={User}
        title="Identity"
        actions={<button className="pp-btn soft" onClick={startEdit}><Edit3 size={13} /> Quick Edit</button>}
      >
        <div className="pp-grid">
          <Field icon={Hash} label="Student Code" value={s.studentCode} mono copyable />
          <Field icon={Hash} label="National ID" value={s.nationalId} mono copyable />
          <Field icon={User} label="Name (Arabic)" value={nameAr} />
          <Field icon={User} label="Name (English)" value={nameEn} />
          <Field icon={Mail} label="Email" value={s.email} copyable />
          <Field icon={Phone} label="Phone" value={s.phoneNumber} copyable />
          <Field icon={Calendar} label="Date of Birth" value={s.birthDate ? `${fmtDate(s.birthDate)}${yearsSince(s.birthDate) !== null ? ` (${yearsSince(s.birthDate)} yrs)` : ""}` : null} />
          <Field icon={UsersIcon} label="Gender" value={s.gender} />
        </div>
      </Panel>

      <Panel icon={UsersIcon} title="Guardian">
        <div className="pp-grid">
          <Field icon={User} label="Guardian Name" value={s.guardianName} />
          <Field icon={Phone} label="Guardian Phone" value={s.guardianPhone} copyable />
        </div>
      </Panel>

      <Panel icon={GraduationCap} title="Academic Placement">
        <div className="pp-grid">
          <Field icon={Building2} label="Faculty" value={s.facultyName} />
          <Field icon={BookOpen} label="Program" value={s.programName} />
          <Field icon={Layers} label="Level" value={s.levelName} />
          <Field icon={Layers} label="Structure Node" value={s.structureNodeName} />
        </div>
      </Panel>
    </>
  );
}

/* ── Finance ────────────────────────────────────────────────── */

const ORDER_STATUS_PILL = { 0: "navy", 1: "info", 2: "good", 3: "bad", 4: "warn", 5: "warn", 6: "bad" };
const ORDER_STATUS_LABEL = { 0: "Created", 1: "Pending Payment", 2: "Paid", 3: "Failed", 4: "Expired", 5: "Refunded", 6: "Cancelled" };
const GATEWAY_LABEL = { 0: "Mastercard", 1: "Bank Misr", 2: "eFinance" };
const FEE_STATUS_LABEL = { 0: "Pending", 1: "In Order", 2: "Paid", 3: "Cancelled", 4: "Refunded" };

function FinanceTab({ studentId, fees, orders, feesQuery, ordersQuery, navigate }) {
  const outstanding = fees.reduce((sum, f) => sum + Number(f.totalAmount ?? 0), 0);
  const currency = fees[0]?.currency || orders[0]?.currency || "EGP";

  return (
    <>
      <Panel
        icon={Receipt}
        title="Outstanding Fees"
        actions={
          <button className="pp-btn gold" onClick={() => navigate(`/admin/finance/treasury?studentId=${studentId}`)}>
            <Wallet size={13} /> Collect in Treasury
          </button>
        }
      >
        {feesQuery.isPending ? (
          <SkeletonCard height={90} />
        ) : fees.length === 0 ? (
          <EmptyState icon={CheckCircle} title="No outstanding fees" message="This student has no unpaid Treasury fees right now." />
        ) : (
          <>
            <p style={{ fontSize: 12, color: "#6b7280", margin: "0 0 10px" }}>
              {fees.length} unpaid fee{fees.length > 1 ? "s" : ""} · total{" "}
              <strong style={{ color: "#b91c1c" }}>{fmtMoney(outstanding, currency)}</strong>
            </p>
            <div style={{ overflowX: "auto" }}>
              <table className="pp-table">
                <thead>
                  <tr><th>Source</th><th>Created</th><th>Qty</th><th>Unit</th><th>Total</th><th>Status</th></tr>
                </thead>
                <tbody>
                  {fees.map((fee) => (
                    <tr key={fee.id}>
                      <td className="mono">{fee.sourceModule || "—"}</td>
                      <td>{fmtDate(fee.createdAt)}</td>
                      <td>{fee.quantity}</td>
                      <td className="num">{fmtMoney(fee.unitAmount, fee.currency)}</td>
                      <td className="num">{fmtMoney(fee.totalAmount, fee.currency)}</td>
                      <td><span className={`pp-pill ${fee.status === 2 ? "good" : fee.status === 1 ? "info" : "warn"}`}>{FEE_STATUS_LABEL[fee.status] ?? "—"}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </Panel>

      <Panel icon={CreditCard} title="Payment Orders">
        {ordersQuery.isPending ? (
          <SkeletonCard height={90} />
        ) : orders.length === 0 ? (
          <EmptyState icon={Landmark} title="No payment orders" message="Orders created at the Treasury or through the student portal will appear here." />
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table className="pp-table">
              <thead>
                <tr><th>Order</th><th>Created</th><th>Gateway</th><th>Fees</th><th>Total</th><th>Status</th></tr>
              </thead>
              <tbody>
                {orders.map((o) => (
                  <tr key={o.id}>
                    <td className="mono">{o.merchantOrderId || o.id.slice(0, 8)}</td>
                    <td>{fmtDateTime(o.createdAt)}</td>
                    <td>{GATEWAY_LABEL[o.gateway] ?? "—"}</td>
                    <td>{o.fees?.length ?? 0}</td>
                    <td className="num">{fmtMoney(o.totalAmount, o.currency)}</td>
                    <td><span className={`pp-pill ${ORDER_STATUS_PILL[o.status] ?? ""}`}>{ORDER_STATUS_LABEL[o.status] ?? "—"}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Panel>
    </>
  );
}

/* ── Records ────────────────────────────────────────────────── */

function RecordsTab({ studentId, records, query, navigate }) {
  return (
    <Panel
      icon={ClipboardCheck}
      title="Profile Records"
      actions={
        <button className="pp-btn soft" onClick={() => navigate(`/admin/students/${studentId}/profile-records`)}>
          <ExternalLink size={13} /> Open Records Manager
        </button>
      }
    >
      {query.isPending ? (
        <SkeletonCard height={90} />
      ) : records.length === 0 ? (
        <EmptyState
          icon={FileText}
          title="No profile records"
          message="Structured records (military status, vaccinations, emergency contacts…) appear here once captured."
        />
      ) : (
        records.map((rec) => {
          const verified = !!rec.verifiedAt;
          return (
            <div className="pp-record" key={rec.id}>
              <div className="pp-record-icon">
                {verified ? <BadgeCheck size={16} /> : <CircleDashed size={16} />}
              </div>
              <div className="pp-record-body">
                <div className="pp-record-title">
                  {getProfileCategoryLabel(rec.category)}
                  {rec.customCategoryKey ? ` — ${rec.customCategoryKey}` : ""}
                </div>
                <div className="pp-record-meta">
                  Updated {fmtDateTime(rec.updatedAt || rec.createdAt)}
                  {verified ? ` · verified ${fmtDate(rec.verifiedAt)}` : " · awaiting verification"}
                  {rec.isSensitive ? " · sensitive" : ""}
                </div>
              </div>
              <span className={`pp-pill ${verified ? "good" : "warn"}`}>
                {verified ? "Verified" : "Unverified"}
              </span>
            </div>
          );
        })
      )}
    </Panel>
  );
}

/* ── Account ────────────────────────────────────────────────── */

function AccountTab({ student: s, onToggleActive, togglePending, onResetPassword, onDelete }) {
  const isExpired = s.passwordStatus === "Expired";
  const expiryDays = daysUntil(s.passwordExpiry);

  return (
    <>
      <Panel icon={ShieldAlert} title="Account & Security">
        <div className="pp-grid">
          <Field icon={Calendar} label="Account Created" value={fmtDateTime(s.createdAt)} />
          <Field
            icon={Key}
            label="Password Status"
            value={
              isExpired
                ? "Expired"
                : s.passwordExpiry
                  ? `Valid — expires ${fmtDate(s.passwordExpiry)}${expiryDays !== null ? ` (${expiryDays} days)` : ""}`
                  : "Valid"
            }
          />
          <Field icon={AlertCircle} label="Account Status" value={s.isActive ? "Active" : "Inactive"} />
        </div>
        <div className="pp-form-actions">
          <button className="pp-btn navy" onClick={onResetPassword}>
            <Key size={13} /> Reset Password
          </button>
        </div>
      </Panel>

      <Panel icon={AlertCircle} title="Danger Zone" className="pp-danger-zone">
        <div className="pp-danger-row">
          <div>
            <h5>{s.isActive ? "Deactivate account" : "Activate account"}</h5>
            <p>{s.isActive ? "The student will no longer be able to sign in." : "Restore the student's ability to sign in."}</p>
          </div>
          <button
            className={`pp-btn ${s.isActive ? "danger" : "success"}`}
            onClick={onToggleActive}
            disabled={togglePending}
          >
            {s.isActive ? <XCircle size={13} /> : <CheckCircle size={13} />}
            {togglePending ? "Working…" : s.isActive ? "Deactivate" : "Activate"}
          </button>
        </div>
        <div className="pp-danger-row">
          <div>
            <h5>Delete student</h5>
            <p>Permanently removes this student and their account. This cannot be undone.</p>
          </div>
          <button className="pp-btn danger" onClick={onDelete}>
            <Trash2 size={13} /> Delete
          </button>
        </div>
      </Panel>
    </>
  );
}

export default StudentDetailPage;
