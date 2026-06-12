import { useState, useCallback, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import {
  UserCog, Plus, Shield, ShieldCheck, Trash2, AlertTriangle, RefreshCw,
  Save, Info, Users, Settings, X, Search, RotateCcw, UserPlus, UserMinus, Loader2,
} from "lucide-react";
import { usePermission } from "../../../core/auth/usePermission";
import { useToast } from "../../../core/components/Toast";
import { getLocalized } from "../../../core/utils/getLocalized";
import * as staffService from "../../../core/services/staffService";
import {
  useRoles, usePermissionTree, useRolePermissions, useRoleMembers,
  useCreateRole, useUpdateRole, useDeleteRole,
  useAddRoleMember, useRemoveRoleMember, useUpdateRolePermissions,
} from "../../../core/query/usePermissionsData";
import {
  LEVEL_TO_ACTION, PERMISSION_RESOURCES, computeResourceLevel,
} from "../../../core/constants/permissionLevels";
import PageHeader from "../../../core/components/PageHeader";
import VirtualList from "../../../core/components/VirtualList";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import LoadingSpinner from "../../../core/components/LoadingSpinner";
import EmptyState from "../../../core/components/EmptyState";
import PermissionMatrix from "../components/PermissionMatrix";
import RoleFormModal from "../components/RoleFormModal";
import "../styles/roles.css";

const MEMBER_VIRTUALIZE_THRESHOLD = 50;
const EMPTY = [];

function RolesPage() {
  const { t, i18n } = useTranslation();
  const { can } = usePermission();
  const { addToast } = useToast();

  // Defensive UI: controls render disabled with an explanation instead of
  // disappearing when the viewer lacks the level.
  const canCreate = can(PERMISSION_RESOURCES.ROLES, 2);
  const canEdit = can(PERMISSION_RESOURCES.ROLES, 3);
  const canDelete = can(PERMISSION_RESOURCES.ROLES, 5);
  const needLevelTitle = (allowed, levelLabel) =>
    (allowed ? undefined : t("requires_permission_level", {
      defaultValue: `Requires "${levelLabel}" access on roles`,
      level: levelLabel,
    }));

  const [selectedRoleId, setSelectedRoleId] = useState(null);
  const [activeTab, setActiveTab] = useState("general");

  const [formName, setFormName] = useState("");
  const [formError, setFormError] = useState("");

  const [pendingLevels, setPendingLevels] = useState({});

  const [deleteTarget, setDeleteTarget] = useState(null);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createError, setCreateError] = useState("");
  const [roleSearch, setRoleSearch] = useState("");

  const [addMemberModalOpen, setAddMemberModalOpen] = useState(false);
  const [memberSearch, setMemberSearch] = useState("");
  const [memberSearchResults, setMemberSearchResults] = useState([]);
  const [memberSearching, setMemberSearching] = useState(false);
  const [removeMemberTarget, setRemoveMemberTarget] = useState(null);
  const memberSearchDebounce = useRef(null);

  // ─── Queries ───
  const rolesQuery = useRoles();
  const roles = rolesQuery.data ?? EMPTY;
  const treeQuery = usePermissionTree();
  const permTree = treeQuery.data ?? EMPTY;
  const rolePermsQuery = useRolePermissions(selectedRoleId, { enabled: activeTab === "permissions" });
  const rolePerms = rolePermsQuery.data;
  const membersQuery = useRoleMembers(selectedRoleId, { enabled: activeTab === "members" });
  const members = membersQuery.data ?? EMPTY;

  // ─── Mutations ───
  const createRole = useCreateRole();
  const updateRole = useUpdateRole();
  const deleteRole = useDeleteRole();
  const addMember = useAddRoleMember();
  const removeMember = useRemoveRoleMember();
  const updateRolePerms = useUpdateRolePermissions();

  const selectedRole = useMemo(
    () => roles.find((r) => r.id === selectedRoleId) || null,
    [roles, selectedRoleId]
  );

  const filteredRoles = useMemo(() => {
    if (!roleSearch.trim()) return roles;
    const q = roleSearch.toLowerCase();
    return roles.filter((r) => r.name.toLowerCase().includes(q));
  }, [roles, roleSearch]);

  // Server truth for the matrix; pendingLevels holds unsaved edits on top.
  const originalLevels = useMemo(() => {
    const levels = {};
    for (const mod of rolePerms || []) {
      for (const res of (mod.resources || [])) {
        levels[`${mod.moduleId}::${res.resourceId}`] = computeResourceLevel(res.permissions || []);
      }
    }
    return levels;
  }, [rolePerms]);

  const displayLevels = useMemo(
    () => ({ ...originalLevels, ...pendingLevels }),
    [originalLevels, pendingLevels]
  );

  const permDirty = Object.keys(pendingLevels).length > 0;

  const handleLevelChange = useCallback((moduleId, res, newLevel) => {
    const compositeKey = `${moduleId}::${res.resourceId}`;
    setPendingLevels((prev) => {
      const original = originalLevels[compositeKey] || 0;
      if (newLevel === original) {
        const next = { ...prev };
        delete next[compositeKey];
        return next;
      }
      return { ...prev, [compositeKey]: newLevel };
    });
  }, [originalLevels]);

  const handleResetPermissions = () => setPendingLevels({});

  const handleSavePermissions = () => {
    if (!selectedRole || updateRolePerms.isPending) return;
    const resources = Object.entries(displayLevels)
      .filter(([, level]) => level > 0)
      .map(([compositeKey, level]) => ({
        resourceId: compositeKey.split("::")[1],
        actions: [LEVEL_TO_ACTION[level]].filter(Boolean),
      }));
    updateRolePerms.mutate({ roleId: selectedRole.id, resources }, {
      onSuccess: () => setPendingLevels({}),
      onError: (err) => addToast(err.message || t("save_failed", { defaultValue: "Failed to save permissions" }), "error"),
    });
  };

  const handleSelectRole = (roleId) => {
    setSelectedRoleId(roleId);
    setActiveTab("general");
    setFormName(roles.find((r) => r.id === roleId)?.name || "");
    setFormError("");
    setPendingLevels({});
  };

  const handleCreate = (name) => {
    setCreateError("");
    createRole.mutate({ name }, {
      onSuccess: () => setCreateModalOpen(false),
      onError: (err) => {
        if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
          setCreateError(t("role_name_exists"));
        } else {
          setCreateError(err.message || "Failed to create role");
        }
      },
    });
  };

  const handleUpdateName = () => {
    if (!selectedRole || updateRole.isPending) return;
    const trimmed = formName.trim();
    if (!trimmed) { setFormError(t("role_name_required")); return; }
    if (trimmed.length < 2) { setFormError(t("role_name_min_length")); return; }
    if (trimmed.length > 100) { setFormError(t("role_name_max_length")); return; }
    updateRole.mutate({ id: selectedRole.id, name: trimmed }, {
      onSuccess: () => setFormError(""),
      onError: (err) => {
        if (err.status === 409 || (err.message && err.message.includes("already exists"))) {
          setFormError(t("role_name_exists"));
        } else {
          setFormError(err.message || "Failed to update role");
        }
      },
    });
  };

  const handleDelete = () => {
    if (!deleteTarget || deleteRole.isPending) return;
    deleteRole.mutate(deleteTarget.id, {
      onSuccess: () => {
        setDeleteTarget(null);
        setSelectedRoleId(null);
      },
      onError: (err) => {
        setDeleteTarget(null);
        addToast(err.message || "Failed to delete role", "error");
      },
    });
  };

  const handleMemberSearch = useCallback((query) => {
    setMemberSearch(query);
    if (memberSearchDebounce.current) clearTimeout(memberSearchDebounce.current);
    if (!query.trim()) {
      setMemberSearchResults([]);
      return;
    }
    memberSearchDebounce.current = setTimeout(async () => {
      setMemberSearching(true);
      try {
        const data = await staffService.searchStaff({ search: query, page: 1, pageSize: 10 });
        const items = data.items || data.data || data || [];
        const results = (Array.isArray(items) ? items : [])
          .filter((s) => !members.some((m) => m.staffId === s.id))
          .map((s) => ({
            id: s.id, name: s.name, email: s.email,
            employeeCode: s.employeeCode, jobTitle: s.jobTitle,
          }));
        setMemberSearchResults(results);
      } catch {
        setMemberSearchResults([]);
      } finally {
        setMemberSearching(false);
      }
    }, 300);
  }, [members]);

  const handleAddMember = (staffId) => {
    if (!selectedRole || addMember.isPending) return;
    addMember.mutate({ roleId: selectedRole.id, staffId }, {
      onSuccess: () => {
        setMemberSearch("");
        setMemberSearchResults([]);
      },
      onError: (err) => addToast(err.message || "Failed to add member", "error"),
    });
  };

  const handleRemoveMember = () => {
    if (!selectedRole || !removeMemberTarget || removeMember.isPending) return;
    removeMember.mutate({ roleId: selectedRole.id, staffId: removeMemberTarget.staffId }, {
      onSuccess: () => setRemoveMemberTarget(null),
      onError: (err) => {
        setRemoveMemberTarget(null);
        addToast(err.message || "Failed to remove member", "error");
      },
    });
  };

  const closeAddMember = () => {
    setAddMemberModalOpen(false);
    setMemberSearch("");
    setMemberSearchResults([]);
  };

  const formatDate = (iso) => {
    if (!iso) return "—";
    try {
      return new Date(iso).toLocaleDateString(i18n.language, { year: "numeric", month: "short", day: "numeric" });
    } catch { return "—"; }
  };

  const totalConfigured = Object.values(displayLevels).filter((l) => l > 0).length;
  const totalResources = (rolePerms || permTree).reduce((sum, m) => sum + (m.resources?.length || 0), 0);

  const renderMemberRow = (m) => (
    <div key={m.id} className="role-member-item">
      <div className="role-member-avatar">{m.name.charAt(0).toUpperCase()}</div>
      <div className="role-member-info">
        <strong>{m.name}</strong>
        <span>{m.email} &middot; {getLocalized(m.jobTitle, i18n.language) || m.employeeCode}</span>
      </div>
      <div className="role-member-meta">
        <span className="role-member-scope-label">{getLocalized(m.structureNodeName, i18n.language) || (m.structureNodeId ? t("structural_scope") : t("global"))}</span>
        {m.year !== "Global" && <span className="role-member-scope">{m.year} / {m.semester}</span>}
      </div>
      <button
        className="role-member-remove-btn"
        onClick={() => canEdit && setRemoveMemberTarget(m)}
        disabled={!canEdit}
        title={needLevelTitle(canEdit, t("edit")) || t("remove_member")}
      >
        <UserMinus size={13} />
      </button>
    </div>
  );

  if (rolesQuery.isPending) {
    return (
      <div className="roles-page">
        <LoadingSpinner message={t("loading_roles")} />
      </div>
    );
  }

  if (rolesQuery.isError && roles.length === 0) {
    return (
      <div className="roles-page">
        <PageHeader icon={UserCog} title={t("roles")} subtitle={t("manage_roles")} />
        <div className="roles-error">
          <AlertTriangle size={36} className="roles-error-icon" />
          <h3>{t("failed_to_load_roles")}</h3>
          <p>{rolesQuery.error?.message}</p>
          <button className="btn-outline" onClick={() => rolesQuery.refetch()}>
            <><RefreshCw size={13} /> {t("retry")}</>
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="roles-page">
      <PageHeader
        icon={UserCog}
        title={t("roles")}
        subtitle={t("manage_roles")}
        actions={
          <button
            className="btn-primary"
            onClick={() => { setCreateError(""); setCreateModalOpen(true); }}
            disabled={!canCreate}
            title={needLevelTitle(canCreate, t("insert"))}
          >
            <><Plus size={14} /> {t("create_role")}</>
          </button>
        }
      />

      <div className="roles-content">
        <div className="roles-sidebar">
          <div className="roles-sidebar-header">
            <span>{t("roles_count", { count: roles.length })}</span>
          </div>
          <div style={{ padding: "6px 8px 0" }}>
            <div style={{
              display: "flex", alignItems: "center", gap: 6,
              background: "var(--color-surface)", border: "1px solid var(--color-border)",
              borderRadius: 6, padding: "6px 10px",
            }}>
              <Search size={13} color="#9ca3af" />
              <input
                type="text"
                placeholder={t("search_roles")}
                value={roleSearch}
                onChange={(e) => setRoleSearch(e.target.value)}
                style={{
                  border: "none", outline: "none", flex: 1,
                  background: "transparent", fontSize: 12,
                  fontFamily: "Outfit, sans-serif", color: "var(--color-primary)",
                }}
              />
            </div>
          </div>
          <div className="roles-sidebar-list">
            {filteredRoles.length === 0 && roleSearch && (
              <div style={{ padding: 20, textAlign: "center", color: "#9ca3af", fontSize: 12 }}>
                {t("no_roles_match", { search: roleSearch })}
              </div>
            )}
            {filteredRoles.map((role) => (
              <button
                key={role.id}
                className={`roles-sidebar-item ${selectedRoleId === role.id ? "active" : ""} ${role.isSystemRole ? "system-role" : ""}`}
                onClick={() => handleSelectRole(role.id)}
              >
                <span className="roles-sidebar-indicator" />
                <span style={{ flex: 1, textAlign: "left" }}>{role.name}</span>
                {role.isSystemRole && (
                  <span style={{ fontSize: 9, opacity: 0.6, textTransform: "uppercase", letterSpacing: 0.5 }}>{t("system_role_badge")}</span>
                )}
              </button>
            ))}
          </div>
        </div>

        <div className="roles-detail">
          {!selectedRole ? (
            <EmptyState
              icon={Shield}
              title={t("select_role")}
              message={t("select_role_desc")}
            />
          ) : (
            <>
              <div className="roles-detail-tabs">
                <button
                  className={`roles-detail-tab ${activeTab === "general" ? "active" : ""}`}
                  onClick={() => setActiveTab("general")}
                >
                  <><Settings size={13} /> {t("general")}</>
                </button>
                <button
                  className={`roles-detail-tab ${activeTab === "permissions" ? "active" : ""}`}
                  onClick={() => setActiveTab("permissions")}
                >
                  <><ShieldCheck size={13} /> {t("permissions")}</>
                  {permDirty && <span style={{ marginLeft: 4, width: 6, height: 6, borderRadius: "50%", background: "var(--gold)", display: "inline-block" }} />}
                </button>
                <button
                  className={`roles-detail-tab ${activeTab === "members" ? "active" : ""}`}
                  onClick={() => setActiveTab("members")}
                >
                  <><Users size={13} /> {t("members")}</>
                </button>
              </div>

              <div className="roles-detail-body">
                {activeTab === "general" && (
                  <div className="role-general-form">
                    <div className="role-form-group">
                      <label>{t("role_name")}</label>
                      <div style={{ display: "flex", gap: 8 }}>
                        <input
                          type="text"
                          className={`role-form-input ${formError ? "error" : ""}`}
                          value={formName}
                          onChange={(e) => setFormName(e.target.value)}
                          placeholder={t("role_name_placeholder")}
                          maxLength={100}
                          style={{ flex: 1 }}
                          disabled={!canEdit}
                        />
                        <button
                          className="btn-primary"
                          onClick={handleUpdateName}
                          disabled={!canEdit || updateRole.isPending || formName === selectedRole.name}
                          title={needLevelTitle(canEdit, t("edit"))}
                        >
                          <Save size={13} /> {updateRole.isPending ? t("saving") : t("save")}
                        </button>
                      </div>
                      {formError && <span className="role-form-error">{formError}</span>}
                      <span className="role-form-hint">{t("role_name_hint")}</span>
                    </div>

                    {selectedRole.isSystemRole && (
                      <div className="role-badge-system">
                        <><Shield size={12} /> {t("system_role")}</>
                      </div>
                    )}

                    <p className="role-meta">
                      <Info size={12} style={{ marginRight: 4, verticalAlign: "middle" }} />
                      {t("created_at")} {formatDate(selectedRole.createdAt)}{selectedRole.isSystemRole ? ` · ${t("managed_by_system")}` : ""}
                    </p>

                    {!selectedRole.isSystemRole && (
                      <div style={{ borderTop: "1px solid var(--color-border)", paddingTop: 16 }}>
                        <h4 style={{ fontSize: 13, fontWeight: 600, margin: "0 0 8px", color: "var(--color-error)" }}>{t("danger_zone")}</h4>
                        <p style={{ fontSize: 12, color: "var(--color-text-secondary)", margin: "0 0 12px" }}>
                          {t("delete_role_warning")}
                        </p>
                        <button
                          className="role-delete-btn"
                          onClick={() => setDeleteTarget(selectedRole)}
                          disabled={!canDelete}
                          title={needLevelTitle(canDelete, t("delete"))}
                        >
                          <><Trash2 size={13} /> {t("delete_role")}</>
                        </button>
                      </div>
                    )}
                  </div>
                )}

                {activeTab === "permissions" && (
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                      <div>
                        <h3 style={{ fontSize: 15, fontWeight: 700, margin: 0, color: "var(--color-primary)" }}>
                          <ShieldCheck size={15} style={{ marginRight: 6, verticalAlign: "middle" }} />
                          {t("role_permissions_title", { name: selectedRole.name })}
                        </h3>
                        <p style={{ fontSize: 12, color: "var(--color-text-secondary)", margin: "4px 0 0" }}>
                          {t("resources_configured", { configured: totalConfigured, total: totalResources })}
                        </p>
                      </div>
                      <div className="roles-header-actions">
                        {permDirty && (
                          <button className="btn-outline" onClick={handleResetPermissions} disabled={updateRolePerms.isPending}>
                            <><RotateCcw size={13} /> {t("reset")}</>
                          </button>
                        )}
                        <button
                          className={`btn-primary ${!permDirty || updateRolePerms.isPending ? "disabled" : ""}`}
                          onClick={handleSavePermissions}
                          disabled={!canEdit || !permDirty || updateRolePerms.isPending}
                          title={needLevelTitle(canEdit, t("edit"))}
                        >
                          {updateRolePerms.isPending ? t("saving") : <><Save size={13} /> {t("save_changes")}</>}
                        </button>
                      </div>
                    </div>

                    {rolePermsQuery.isPending ? (
                      <div className="roles-loading" style={{ padding: "40px 0" }}>
                        <div className="roles-spinner" />
                        <p>{t("loading_permissions")}</p>
                      </div>
                    ) : (
                      <PermissionMatrix
                        modules={rolePerms || permTree}
                        getLevel={(moduleId, res) => displayLevels[`${moduleId}::${res.resourceId}`] || 0}
                        onLevelChange={handleLevelChange}
                        canEdit={canEdit}
                        disabledReason={needLevelTitle(false, t("edit"))}
                      />
                    )}
                  </div>
                )}

                {activeTab === "members" && (
                  <div>
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 14 }}>
                      <div>
                        <h3 style={{ fontSize: 15, fontWeight: 700, margin: 0, color: "var(--color-primary)" }}>
                          <Users size={15} style={{ marginRight: 6, verticalAlign: "middle" }} />
                          {t("members_title", { name: selectedRole.name })}
                        </h3>
                        <p style={{ fontSize: 12, color: "var(--color-text-secondary)", margin: "4px 0 0" }}>
                          {t("members_count", { count: members.length })}
                        </p>
                      </div>
                      <button
                        className="btn-primary"
                        onClick={() => setAddMemberModalOpen(true)}
                        disabled={!canEdit}
                        title={needLevelTitle(canEdit, t("edit"))}
                      >
                        <><UserPlus size={13} /> {t("add_member")}</>
                      </button>
                    </div>
                    <div className="role-members-list">
                      {membersQuery.isPending ? (
                        <div className="roles-loading" style={{ padding: "40px 0" }}>
                          <div className="roles-spinner" />
                          <p>{t("loading_members")}</p>
                        </div>
                      ) : members.length === 0 ? (
                        <div className="role-members-empty">
                          <Users size={32} color="#d1d5db" style={{ marginBottom: 8 }} />
                          <p>{t("no_members")}</p>
                        </div>
                      ) : members.length > MEMBER_VIRTUALIZE_THRESHOLD ? (
                        <VirtualList
                          items={members}
                          rowHeight={60}
                          height={420}
                          rowKey={(m) => m.id}
                          renderRow={(m) => renderMemberRow(m)}
                        />
                      ) : (
                        members.map(renderMemberRow)
                      )}
                    </div>
                  </div>
                )}
              </div>
            </>
          )}
        </div>
      </div>

      {createModalOpen && (
        <RoleFormModal
          title={t("create_role")}
          submitLabel={t("create")}
          pending={createRole.isPending}
          serverError={createError}
          onSubmit={handleCreate}
          onClose={() => setCreateModalOpen(false)}
        />
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => { if (!deleteRole.isPending) setDeleteTarget(null); }}
        onConfirm={handleDelete}
        title={t("delete_role")}
        message={deleteTarget ? t("delete_role_confirm", { name: deleteTarget.name }) : ""}
        detail={t("delete_role_warning")}
        confirmLabel={t("delete")}
        cancelLabel={t("cancel")}
        variant="danger"
        loading={deleteRole.isPending}
      />

      <ConfirmDialog
        open={!!removeMemberTarget}
        onClose={() => { if (!removeMember.isPending) setRemoveMemberTarget(null); }}
        onConfirm={handleRemoveMember}
        title={t("remove_member")}
        message={removeMemberTarget ? t("remove_member_confirm", { name: removeMemberTarget.name }) : ""}
        detail={t("remove_member_warning", { role: selectedRole?.name })}
        confirmLabel={t("remove")}
        cancelLabel={t("cancel")}
        variant="warning"
        loading={removeMember.isPending}
      />

      {addMemberModalOpen && selectedRole && (
        <div className="roles-modal-overlay" onClick={closeAddMember}>
          <div className="roles-modal roles-modal-wide" onClick={(e) => e.stopPropagation()}>
            <div className="roles-modal-header">
              <div className="roles-modal-header-left">
                <UserPlus size={18} color="var(--color-primary)" />
                <div>
                  <h2>{t("add_member_to_role", { name: selectedRole.name })}</h2>
                  <p className="roles-modal-subtitle">{t("add_member_subtitle")}</p>
                </div>
              </div>
              <button className="roles-modal-close" onClick={closeAddMember}><X size={16} /></button>
            </div>
            <div className="roles-modal-body">
              <div className="role-add-member-search">
                <Search size={14} color="#9ca3af" />
                <input
                  type="text"
                  className="role-add-member-input"
                  value={memberSearch}
                  onChange={(e) => handleMemberSearch(e.target.value)}
                  placeholder={t("search_staff_placeholder")}
                  autoFocus
                />
                {memberSearching && <Loader2 size={14} className="roles-spinner-inline" />}
              </div>
              <div className="role-add-member-results">
                {memberSearchResults.length === 0 && memberSearch.trim() && !memberSearching ? (
                  <div className="role-add-member-empty">
                    <Users size={24} color="#d1d5db" />
                    <p>{t("no_staff_found")}</p>
                  </div>
                ) : memberSearchResults.length === 0 && !memberSearch.trim() && !memberSearching ? (
                  <div className="role-add-member-empty">
                    <UserPlus size={24} color="#d1d5db" />
                    <p>{t("type_to_search_staff")}</p>
                  </div>
                ) : (
                  memberSearchResults.map((s) => (
                    <div key={s.id} className="role-add-member-result-item">
                      <div className="role-member-avatar">
                        {s.name.charAt(0).toUpperCase()}
                      </div>
                      <div className="role-member-info">
                        <strong>{s.name}</strong>
                        <span>{s.email} &middot; {getLocalized(s.jobTitle, i18n.language) || s.employeeCode}</span>
                      </div>
                      <button
                        className="role-add-member-add-btn"
                        onClick={() => handleAddMember(s.id)}
                        disabled={addMember.isPending}
                      >
                        {addMember.isPending ? (
                          <Loader2 size={13} className="roles-spinner-inline" />
                        ) : (
                          <><UserPlus size={13} /> {t("add")}</>
                        )}
                      </button>
                    </div>
                  ))
                )}
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default RolesPage;
