import { useState, useEffect, useMemo, useCallback } from "react";
import { useParams, useNavigate, Navigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  User, Mail, Phone, Calendar, Hash, Briefcase, Building2, Layers,
  Edit3, Key, Trash2, ArrowLeft, Clock, Shield, ShieldCheck, ShieldAlert,
  CheckCircle, XCircle, AlertCircle, Search, ChevronDown, ChevronUp,
  GraduationCap, Users as UsersIcon, BadgeCheck, Lock,
} from "lucide-react";
import { useToast } from "../../../core/components/Toast";
import { useStickySelection } from "../../../core/contexts/StickySelectionContext";
import { getLocalized, parseLocalizedValue } from "../../../core/utils/getLocalized";
import { fetchUserPermissionTree } from "../../../core/services/authorizationService";
import { SkeletonCard, SkeletonStats } from "../../../core/components/Skeleton";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import { PhotoValidationOverlay } from "../../../core/components/PhotoValidationOverlay";
import { usePhotoValidator } from "../../../core/hooks/usePhotoValidator";
import ErrorMessage from "../components/ErrorMessage";
import userService from "../services/userService";
import {
  ProfileHero, StatCard, TabBar, Panel, Field, EmptyState,
  ResetPasswordModal, CopyButton,
} from "../components/ProfileKit";
import {
  fmtDate, fmtDateTime, yearsSince, daysUntil, resolvePhotoUrl,
} from "../components/profileUtils";

const staffKey = (id) => ["staff", id];

/**
 * The update endpoint replaces the whole entity, so partial edits must echo
 * the current values back (same contract as the student endpoint).
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
    role: s.role || "",
    jobTitle: s.jobTitle || "",
    photoUrl: s.photoUrl ?? null,
    gender: s.gender ?? null,
    qualification: s.qualification ?? null,
    structureNodeId: s.structureNodeId,
    isActive: s.isActive,
    ...overrides,
  };
}

function UserDetails() {
  const { id } = useParams();
  // Remount per user so tab state never leaks between profiles.
  return <StaffDetailContent key={id} id={id} />;
}

function StaffDetailContent({ id }) {
  const navigate = useNavigate();
  const { i18n } = useTranslation();
  const { addToast } = useToast();
  const { select } = useStickySelection();
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState("overview");
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [resetOpen, setResetOpen] = useState(false);

  const staffQuery = useQuery({
    queryKey: staffKey(id),
    queryFn: () => userService.getStaffById(id),
    retry: false,
    enabled: !!id,
  });
  const staff = staffQuery.data;

  // Directory "All users" links land here for students too — detect and
  // forward them to the dedicated student profile.
  const studentProbe = useQuery({
    queryKey: ["student-probe", id],
    queryFn: () => userService.getStudentById(id),
    retry: false,
    enabled: !!id && staffQuery.isError,
  });

  // The permission tree endpoint is staff-only; errors (403/404) collapse to
  // an explanatory empty state.
  const permTreeQuery = useQuery({
    queryKey: ["user-permission-tree", id],
    queryFn: () => fetchUserPermissionTree(id),
    retry: false,
    enabled: !!staff?.id,
  });
  const permTree = useMemo(
    () => (Array.isArray(permTreeQuery.data) ? permTreeQuery.data : []),
    [permTreeQuery.data],
  );

  const permStats = useMemo(() => {
    let granted = 0;
    let total = 0;
    let denies = 0;
    permTree.forEach((m) => m.resources?.forEach((r) => r.permissions?.forEach((p) => {
      total += 1;
      if (p.isAssigned) granted += 1;
      if (p.hasDenyOverride) denies += 1;
    })));
    return { granted, total, denies };
  }, [permTree]);

  const updateStaff = useMutation({
    mutationFn: (body) => userService.updateStaff(id, body),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: staffKey(id) });
      queryClient.invalidateQueries({ queryKey: ["directory"] });
    },
  });

  const toggleStatus = useMutation({
    mutationFn: () => userService.toggleStaffStatus(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: staffKey(id) });
      queryClient.invalidateQueries({ queryKey: ["directory"] });
      addToast(`Staff member ${staff?.isActive ? "deactivated" : "activated"}`, "success");
    },
    onError: (err) => addToast(err.message, "error"),
  });

  const deleteStaff = useMutation({
    mutationFn: () => userService.deleteStaff(id),
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: staffKey(id) });
      queryClient.invalidateQueries({ queryKey: ["directory"] });
      addToast("Staff member deleted", "success");
      navigate("/admin/users");
    },
    onError: (err) => {
      setDeleteOpen(false);
      addToast(err.message, "error");
    },
  });

  const uploadPhoto = useMutation({
    mutationFn: (file) => userService.uploadStaffPhoto(id, file),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: staffKey(id) });
      addToast("Photo updated", "success");
    },
    onError: (err) => addToast(err.message, "error"),
  });

  const photoValidator = usePhotoValidator();
  const [photoValidationFile, setPhotoValidationFile] = useState(null);
  const [showPhotoValidation, setShowPhotoValidation] = useState(false);

  const handlePhotoUpload = useCallback(async (file) => {
    await photoValidator.loadModel();
    photoValidator.reset();
    setPhotoValidationFile(file);
    setShowPhotoValidation(true);
    const result = await photoValidator.validate(file);
    if (result?.passed) {
      uploadPhoto.mutate(file);
      setShowPhotoValidation(false);
      setPhotoValidationFile(null);
    }
  }, [photoValidator, uploadPhoto]);

  const handleAcceptPhoto = useCallback(() => {
    if (photoValidationFile) {
      uploadPhoto.mutate(photoValidationFile);
    }
    setShowPhotoValidation(false);
    setPhotoValidationFile(null);
  }, [photoValidationFile, uploadPhoto]);

  const handleRejectPhoto = useCallback(() => {
    setShowPhotoValidation(false);
    setPhotoValidationFile(null);
    photoValidator.reset();
  }, [photoValidator]);

  // Keep the directory sidebar pin in sync with this profile.
  const localizedName = staff ? getLocalized(staff.name, i18n.language) : "";
  useEffect(() => {
    if (staff?.id) {
      select({ id: staff.id, name: localizedName, code: staff.employeeCode, type: "staff" });
    }
  }, [staff?.id, localizedName, staff?.employeeCode, select]);

  const handleResetPassword = (password) =>
    new Promise((resolve) => {
      updateStaff.mutate(buildUpdatePayload(staff, { password, confirmPassword: password }), {
        onSuccess: () => {
          addToast("Password has been reset", "success");
          setResetOpen(false);
          resolve();
        },
        onError: (err) => {
          addToast(err.response?.data?.message || err.message, "error");
          resolve();
        },
      });
    });

  if (studentProbe.data?.id) {
    return <Navigate to={`/admin/students/${id}`} replace />;
  }

  if (staffQuery.isPending || (staffQuery.isError && studentProbe.isPending)) {
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

  if (staffQuery.isError || !staff) {
    return <ErrorMessage message={staffQuery.error?.message || "User not found"} />;
  }

  const s = staff;
  const isExpired = s.passwordStatus === "Expired";
  const expiryDays = daysUntil(s.passwordExpiry);

  const tabs = [
    { id: "overview", label: "Overview", icon: User },
    { id: "permissions", label: "Access & Permissions", icon: ShieldCheck, count: permStats.total ? permStats.granted : null },
    { id: "account", label: "Account", icon: ShieldAlert },
  ];

  return (
    <div className="pp-page">
      <div className="pp-topbar">
        <button className="pp-back" onClick={() => navigate("/admin/users")}>
          <ArrowLeft size={13} /> Staff Directory
        </button>
      </div>

      <ProfileHero
        photoUrl={resolvePhotoUrl(s.photoUrl)}
        initial={(localizedName || "U").charAt(0).toUpperCase()}
        name={localizedName}
        subtitle={
          <>
            <span style={{ fontFamily: "Space Mono, monospace" }}>{s.employeeCode}</span>
            <span>·</span>
            <span>{s.email}</span>
            <CopyButton value={s.email} label="Email" />
          </>
        }
        badges={
          <>
            <span className="pp-badge tone-staff"><Briefcase size={10} /> Staff</span>
            {s.role && <span className="pp-badge tone-gold"><Shield size={10} /> {s.role}</span>}
            <span className={`pp-badge ${s.isActive ? "tone-good" : "tone-bad"}`}>
              <span className="pp-badge-dot" /> {s.isActive ? "Active" : "Inactive"}
            </span>
            {isExpired && <span className="pp-badge tone-bad"><Key size={10} /> Password Expired</span>}
          </>
        }
        chips={
          <>
            {s.jobTitle && <span className="pp-chip"><Briefcase size={11} /> {s.jobTitle}</span>}
            {(s.facultyName || s.structureNodeName) && (
              <span className="pp-chip"><Building2 size={11} /> {s.facultyName || s.structureNodeName}</span>
            )}
          </>
        }
        actions={
          <>
            <button className="pp-hero-btn primary" onClick={() => navigate(`/admin/users/staff/${id}/edit`)}>
              <Edit3 size={13} /> Edit Staff
            </button>
            <button className="pp-hero-btn ghost" onClick={() => setResetOpen(true)}>
              <Key size={13} /> Reset Password
            </button>
          </>
        }
        onUploadPhoto={handlePhotoUpload}
        uploading={uploadPhoto.isPending}
        validating={photoValidator.isProcessing || photoValidator.modelLoading}
        validationOverlay={showPhotoValidation && photoValidator.results && (
          <PhotoValidationOverlay
            results={photoValidator.results}
            previewUrl={photoValidationFile ? URL.createObjectURL(photoValidationFile) : null}
            isProcessing={photoValidator.isProcessing}
            error={photoValidator.error}
            onAccept={handleAcceptPhoto}
            onReject={handleRejectPhoto}
            onRetry={() => photoValidator.validate(photoValidationFile)}
          />
        )}
      />

      <div className="pp-stats pp-fade">
        <StatCard icon={Shield} label="Role" value={s.role || "—"} hint={s.jobTitle || undefined} tone="gold" />
        <StatCard
          icon={ShieldCheck}
          label="Permissions"
          value={permTreeQuery.isPending ? "…" : permStats.total ? `${permStats.granted} granted` : "—"}
          hint={
            permStats.total
              ? `of ${permStats.total} possible${permStats.denies ? ` · ${permStats.denies} denied` : ""}`
              : "Open the permissions tab"
          }
          onClick={() => setActiveTab("permissions")}
        />
        <StatCard
          icon={Key}
          label="Password"
          value={isExpired ? "Expired" : expiryDays !== null ? `${expiryDays} days left` : "Valid"}
          hint={s.passwordExpiry ? `Expires ${fmtDate(s.passwordExpiry)}` : "No expiry set"}
          tone={isExpired ? "danger" : ""}
          onClick={() => setActiveTab("account")}
        />
        <StatCard icon={Clock} label="Member Since" value={fmtDate(s.createdAt)} />
      </div>

      <TabBar tabs={tabs} active={activeTab} onChange={setActiveTab} />

      <div className="pp-fade" key={activeTab}>
        {activeTab === "overview" && <OverviewTab staff={s} />}
        {activeTab === "permissions" && <PermissionsTab query={permTreeQuery} tree={permTree} stats={permStats} />}
        {activeTab === "account" && (
          <AccountTab
            staff={s}
            onToggleActive={() => toggleStatus.mutate()}
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
        pending={updateStaff.isPending}
      />

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => { if (!deleteStaff.isPending) setDeleteOpen(false); }}
        onConfirm={() => deleteStaff.mutate()}
        title="Delete Staff Member"
        message={`Permanently delete ${localizedName}?`}
        detail="This cannot be undone."
        confirmLabel="Delete"
        variant="danger"
        loading={deleteStaff.isPending}
      />
    </div>
  );
}

/* ── Overview ───────────────────────────────────────────────── */

function OverviewTab({ staff: s }) {
  const { ar: nameAr, en: nameEn } = parseLocalizedValue(s.name);
  const age = yearsSince(s.birthDate);

  return (
    <>
      <Panel icon={User} title="Identity">
        <div className="pp-grid">
          <Field icon={Hash} label="Employee Code" value={s.employeeCode} mono copyable />
          <Field icon={Hash} label="National ID" value={s.nationalId} mono copyable />
          <Field icon={User} label="Name (Arabic)" value={nameAr} />
          <Field icon={User} label="Name (English)" value={nameEn} />
          <Field icon={Mail} label="Email" value={s.email} copyable />
          <Field icon={Phone} label="Phone" value={s.phoneNumber} copyable />
          <Field icon={Calendar} label="Date of Birth" value={s.birthDate ? `${fmtDate(s.birthDate)}${age !== null ? ` (${age} yrs)` : ""}` : null} />
          <Field icon={UsersIcon} label="Gender" value={s.gender} />
        </div>
      </Panel>

      <Panel icon={Briefcase} title="Employment">
        <div className="pp-grid">
          <Field icon={Shield} label="Role" value={s.role} />
          <Field icon={Briefcase} label="Job Title" value={s.jobTitle} />
          <Field icon={Building2} label="Faculty / Department" value={s.facultyName || s.structureNodeName} />
          <Field icon={Layers} label="Structure Node" value={s.structureNodeName} />
          <Field icon={GraduationCap} label="Qualification" value={s.qualification} />
        </div>
      </Panel>
    </>
  );
}

/* ── Permissions ────────────────────────────────────────────── */

function PermissionsTab({ query, tree, stats }) {
  const [search, setSearch] = useState("");
  const [openModules, setOpenModules] = useState(() => new Set());

  const filtered = useMemo(() => {
    if (!search) return tree;
    const q = search.toLowerCase();
    return tree
      .map((m) => {
        const moduleHit = m.moduleName?.toLowerCase().includes(q);
        const resources = (m.resources || [])
          .map((r) => {
            const resourceHit = r.resourceName?.toLowerCase().includes(q);
            const permissions = (r.permissions || []).filter(
              (p) =>
                resourceHit || moduleHit ||
                p.action?.toLowerCase().includes(q) ||
                p.permissionName?.toLowerCase().includes(q),
            );
            return permissions.length ? { ...r, permissions } : null;
          })
          .filter(Boolean);
        return resources.length ? { ...m, resources } : null;
      })
      .filter(Boolean);
  }, [tree, search]);

  const toggleModule = (moduleId) =>
    setOpenModules((prev) => {
      const next = new Set(prev);
      if (next.has(moduleId)) next.delete(moduleId);
      else next.add(moduleId);
      return next;
    });

  if (query.isPending) {
    return <Panel icon={ShieldCheck} title="Effective Permissions"><SkeletonCard height={180} /></Panel>;
  }

  if (query.isError || tree.length === 0) {
    return (
      <Panel icon={ShieldCheck} title="Effective Permissions">
        <EmptyState
          icon={Lock}
          title="Permission tree unavailable"
          message="You may not have access to view this user's permissions, or none have been assigned yet."
        />
      </Panel>
    );
  }

  return (
    <Panel
      icon={ShieldCheck}
      title="Effective Permissions"
      actions={
        <>
          <span className="pp-pill navy">{stats.granted} granted</span>
          {stats.denies > 0 && <span className="pp-pill bad">{stats.denies} denied</span>}
        </>
      }
    >
      <div className="pp-perm-summary">
        <div className="pp-perm-search">
          <Search size={13} />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Filter by module, resource or action…"
          />
        </div>
      </div>

      {filtered.length === 0 && (
        <EmptyState icon={Search} title="No matches" message="No permission matches that filter." />
      )}

      {filtered.map((mod) => {
        // While filtering, every matching module is expanded for scanning.
        const isOpen = !!search || openModules.has(mod.moduleId);
        const counts = (mod.resources || []).reduce(
          (acc, r) => {
            (r.permissions || []).forEach((p) => {
              acc.total += 1;
              if (p.isAssigned) acc.granted += 1;
            });
            return acc;
          },
          { granted: 0, total: 0 },
        );
        const pct = counts.total ? Math.round((counts.granted / counts.total) * 100) : 0;

        return (
          <div className="pp-perm-module" key={mod.moduleId}>
            <button type="button" className="pp-perm-module-head" onClick={() => toggleModule(mod.moduleId)}>
              {isOpen ? <ChevronUp size={14} /> : <ChevronDown size={14} />}
              <span style={{ flex: "0 1 auto" }}>{mod.moduleName}</span>
              <div className="pp-perm-module-meter"><span style={{ width: `${pct}%` }} /></div>
              <span className="pp-pill navy">{counts.granted}/{counts.total}</span>
            </button>
            {isOpen && (
              <div className="pp-perm-resources">
                {(mod.resources || []).map((res) => (
                  <div className="pp-perm-resource" key={res.resourceId}>
                    <div className="pp-perm-resource-name">{res.resourceName}</div>
                    <div className="pp-perm-actions">
                      {(res.permissions || []).map((p) => {
                        const cls = p.hasDenyOverride
                          ? "deny"
                          : p.hasAllowOverride
                            ? "allow"
                            : p.isAssigned
                              ? "granted"
                              : "";
                        const title = p.hasDenyOverride
                          ? "Explicitly denied in at least one scope"
                          : p.hasAllowOverride
                            ? "Explicitly allowed in at least one scope"
                            : p.isAssigned
                              ? p.description || "Granted via role"
                              : "Not granted";
                        return (
                          <span className={`pp-perm-action ${cls}`} key={p.permissionId} title={title}>
                            {p.action}
                          </span>
                        );
                      })}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        );
      })}
    </Panel>
  );
}

/* ── Account ────────────────────────────────────────────────── */

function AccountTab({ staff: s, onToggleActive, togglePending, onResetPassword, onDelete }) {
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
          <Field icon={BadgeCheck} label="Account Status" value={s.isActive ? "Active" : "Inactive"} />
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
            <p>{s.isActive ? "The staff member will no longer be able to sign in." : "Restore this staff member's ability to sign in."}</p>
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
            <h5>Delete staff member</h5>
            <p>Permanently removes this staff member and their account. This cannot be undone.</p>
          </div>
          <button className="pp-btn danger" onClick={onDelete}>
            <Trash2 size={13} /> Delete
          </button>
        </div>
      </Panel>
    </>
  );
}

export default UserDetails;
