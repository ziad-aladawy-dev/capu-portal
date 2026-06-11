import { useState, useEffect, useRef } from "react";
import { Shield, User, Search, X, Check, ShieldCheck } from "lucide-react";
import { useTranslation } from "react-i18next";
import * as staffService from "../../../core/services/staffService";
import * as studentService from "../../../core/services/studentService";
import { useUserScope } from "../../../core/hooks/useUserScope";
import {
  useUserPermissionTree, usePermissionAssignment,
} from "../../../core/query/usePermissionsData";
import PermissionsEditor from "../components/PermissionsEditor";
import "../styles/permissions.css";
import "../styles/roles.css";

function PermissionsPage() {
  const { scopedUser, isScoped, scopeToUser } = useUserScope();
  const { t } = useTranslation();

  // The pinned user from the directory sidebar IS the selection — deriving it
  // (instead of mirroring into local state) keeps page and sidebar in sync.
  const selectedUser = isScoped ? scopedUser : null;

  const [searchQuery, setSearchQuery] = useState("");
  const [searchResults, setSearchResults] = useState([]);
  const [searching, setSearching] = useState(false);
  const [searchDone, setSearchDone] = useState(false);
  const debounceRef = useRef(null);

  // Event-driven debounce: no effects, just the input handler + unmount cleanup.
  useEffect(() => () => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
  }, []);

  const handleSearchInput = (value) => {
    setSearchQuery(value);
    setSearchDone(false);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (!value.trim()) {
      setSearchResults([]);
      setSearching(false);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      setSearching(true);
      try {
        const [staffRes, studentRes] = await Promise.all([
          staffService.searchStaff({ search: value, page: 1, pageSize: 10 }),
          studentService.searchStudents({ search: value, page: 1, pageSize: 10 }),
        ]);
        const staff = (staffRes?.items || []).map((s) => ({ id: s.id, name: s.name, code: s.employeeCode, type: "staff" }));
        const students = (studentRes?.items || []).map((s) => ({ id: s.id, name: s.name, code: s.studentCode, type: "student" }));
        setSearchResults([...staff, ...students]);
      } catch { setSearchResults([]); }
      finally {
        setSearching(false);
        setSearchDone(true);
      }
    }, 300);
  };

  const handleSelectUser = (user) => {
    scopeToUser(user);
    setSearchQuery("");
    setSearchResults([]);
  };

  const isStudentSelected = selectedUser?.type === "student";

  // Students are context-scoped on the backend: they hold no StaffRoles /
  // StaffPermissions rows, and the permission-tree endpoint only resolves
  // staff ids (404 otherwise). Never request the tree for a student.
  const staffUserId = selectedUser && !isStudentSelected ? selectedUser.id : null;

  // Both keys carry the active scope, so flipping the navbar scope refetches
  // and remounts the editor with scope-correct data (Scenario B).
  const treeQuery = useUserPermissionTree(staffUserId);
  const assignmentQuery = usePermissionAssignment(staffUserId);

  const ready = !!staffUserId && treeQuery.isSuccess && assignmentQuery.isSuccess;
  const loading = !!staffUserId && (treeQuery.isPending || assignmentQuery.isPending);
  const loadError = !!staffUserId && (treeQuery.isError || assignmentQuery.isError);

  const selectedUserName = selectedUser?.name || "";
  const selectedUserCode = selectedUser?.code || "";
  const selectedUserType = selectedUser?.type || "";

  return (
    <div className="perm-page">
      <div className="perm-header">
        <div className="perm-header-left">
          <Shield size={20} />
          <div>
            <h1>{t("permissions_manager")}</h1>
            <p>{t("permissions_manager_desc")}</p>
          </div>
        </div>
      </div>

      <div className="perm-layout">
        <div className="perm-search-box" style={{ display: "flex", alignItems: "center", gap: 8, padding: "8px 12px", background: "#f4f5f7", borderRadius: 8, color: "#6b7280" }}>
          <Search size={14} />
          <input
            type="text"
            placeholder={t("search_users_placeholder") || "Search users by name or ID…"}
            value={searchQuery}
            onChange={(e) => handleSearchInput(e.target.value)}
            style={{ flex: 1, border: "none", background: "none", fontSize: 13, fontFamily: "Outfit, sans-serif", outline: "none", color: "#1a1f5e" }}
          />
          {searchQuery && <button style={{ background: "none", border: "none", cursor: "pointer", color: "#6b7280" }} onClick={() => handleSearchInput("")}><X size={12} /></button>}
        </div>
        {searching && <div style={{ fontSize: 12, color: "#6b7280", padding: "8px 4px" }}>{t("searching") || "Searching…"}</div>}
        {searchDone && !searching && searchQuery.trim() && searchResults.length === 0 && (
          <div style={{ fontSize: 12, color: "#6b7280", padding: "8px 4px" }}>
            {t("no_users_found_in_scope", {
              defaultValue: "No users match this search. Note: results are limited to the active scope.",
            })}
          </div>
        )}
        {searchResults.length > 0 && (
          <div style={{ maxHeight: 280, overflowY: "auto" }}>
            {searchResults.map((u) => (
              <button
                key={`${u.type}-${u.id}`}
                style={{ display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "8px 10px", border: "none", background: selectedUser?.id === u.id ? "#e8eaf6" : "none", cursor: "pointer", borderRadius: 8, textAlign: "left", fontFamily: "Outfit, sans-serif", color: "#1a1f5e", transition: "background 0.15s" }}
                onClick={() => handleSelectUser(u)}
              >
                <User size={14} />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <strong style={{ display: "block", fontSize: 13, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>{u.name}</strong>
                  <span style={{ display: "block", fontSize: 11, color: "#6b7280" }}>{u.code} &middot; {u.type === "staff" ? t("staff") : t("student")}</span>
                </div>
                {selectedUser?.id === u.id && <Check size={13} />}
              </button>
            ))}
          </div>
        )}

        {selectedUser ? (
          <div className="perm-user-card">
            <div className="perm-user-avatar">{selectedUserName.charAt(0)}</div>
            <div className="perm-user-info">
              <strong>{selectedUserName}</strong>
              <span>{selectedUserCode} &middot; {selectedUserType === "staff" ? t("staff") : t("student")}</span>
            </div>
          </div>
        ) : (
          <div className="perm-empty-state">
            <User size={36} />
            <h3>{t("select_user")}</h3>
            <p>{t("select_user_desc")}</p>
          </div>
        )}

        {isStudentSelected && (
          <div className="perm-empty-state">
            <ShieldCheck size={36} />
            <h3>{t("student_permissions_unavailable")}</h3>
            <p>{t("student_permissions_unavailable_desc")}</p>
          </div>
        )}

        {loading && (
          <div className="roles-loading" style={{ padding: 40 }}>
            <div className="roles-spinner" />
            <p>{t("loading_permissions")}</p>
          </div>
        )}

        {loadError && (
          <div className="perm-empty-state">
            <Shield size={36} />
            <h3>{t("load_failed")}</h3>
            <p>{treeQuery.error?.message || assignmentQuery.error?.message}</p>
          </div>
        )}

        {ready && (
          <PermissionsEditor
            key={`${staffUserId}:${treeQuery.dataUpdatedAt}:${assignmentQuery.dataUpdatedAt}`}
            user={selectedUser}
            tree={Array.isArray(treeQuery.data) ? treeQuery.data : []}
            assignment={assignmentQuery.data}
          />
        )}
      </div>
    </div>
  );
}

export default PermissionsPage;
