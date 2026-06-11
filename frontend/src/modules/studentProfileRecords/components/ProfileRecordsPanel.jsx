import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  FileText, Plus, Edit2, Trash2, X, AlertTriangle, ShieldCheck,
  RefreshCw, Lock,
} from "lucide-react";
import * as studentProfileService from "../../../core/services/studentProfileService";
import { useAuth } from "../../../core/auth/useAuth";
import { usePermission } from "../../../core/auth/usePermission";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import ProfileRecordFormModal from "./ProfileRecordFormModal";
import { prettifyJson } from "./recordUtils";
import "../styles/studentProfileRecords.css";

const RESOURCE = "student-information.profile-records";

const recordsKey = (studentId) => ["profile-records", studentId];

// The full profile-records experience for one student: query + mutations,
// records grid, create/edit modal and delete confirmation. Both the
// entity-scoped page and the standalone management page render this panel.
function ProfileRecordsPanel({ studentId }) {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { can } = usePermission();
  const queryClient = useQueryClient();

  const canAdd = can(RESOURCE, 2);
  const canModify = can(RESOURCE, 3);
  const canDelete = can(RESOURCE, 5);
  const deniedTitle = (allowed, level) =>
    (allowed ? undefined : t("requires_permission_level", {
      defaultValue: `Requires "${level}" access on profile records`,
      level,
    }));

  const [modalMode, setModalMode] = useState(null); // 'create' | 'edit'
  const [editRecord, setEditRecord] = useState(null);
  const [serverError, setServerError] = useState("");
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [error, setError] = useState(null);

  const recordsQuery = useQuery({
    queryKey: recordsKey(studentId),
    queryFn: () => studentProfileService.fetchProfileRecords(studentId),
    select: (data) => (Array.isArray(data) ? data : []),
    enabled: !!studentId,
  });
  const records = recordsQuery.data || [];

  const invalidate = () => queryClient.invalidateQueries({ queryKey: recordsKey(studentId) });

  const upsertMutation = useMutation({
    mutationFn: (payload) => studentProfileService.upsertProfileRecord(studentId, payload),
    onSuccess: () => {
      setModalMode(null);
      setEditRecord(null);
      setServerError("");
      invalidate();
    },
    onError: (err) => setServerError(err.message || "Failed to save record"),
  });

  const verifyMutation = useMutation({
    mutationFn: (record) => studentProfileService.verifyProfileRecord(studentId, record.id, user.id),
    onSuccess: invalidate,
    onError: (err) => setError(err.message || "Failed to verify record"),
  });

  const deleteMutation = useMutation({
    mutationFn: (record) => studentProfileService.deleteProfileRecord(studentId, record.id),
    onSuccess: () => {
      setDeleteTarget(null);
      invalidate();
    },
    onError: (err) => {
      setDeleteTarget(null);
      setError(err.message || "Failed to delete record");
    },
  });

  if (!studentId) return null;

  const openCreate = () => {
    setModalMode("create");
    setEditRecord(null);
    setServerError("");
  };

  const openEdit = (record) => {
    setModalMode("edit");
    setEditRecord(record);
    setServerError("");
  };

  const handleVerify = (record) => {
    if (!user?.id) {
      setError("Cannot verify — current user id unknown.");
      return;
    }
    if (verifyMutation.isPending) return;
    verifyMutation.mutate(record);
  };

  return (
    <>
      {error && (
        <div className="spr-error-banner">
          <AlertTriangle size={16} />
          <span>{error}</span>
          <button
            onClick={() => setError(null)}
            style={{ marginLeft: "auto", background: "transparent", border: "none", cursor: "pointer", color: "#b91c1c" }}
          >
            <X size={14} />
          </button>
        </div>
      )}

      {recordsQuery.isPending ? (
        <div className="spr-loading">
          <div className="spr-spinner" />
          <p>{t("loading_profile_records")}</p>
        </div>
      ) : recordsQuery.isError ? (
        <div className="spr-error-banner">
          <AlertTriangle size={16} />
          <span>{recordsQuery.error?.message || "Failed to load profile records"}</span>
          <button
            className="spr-btn spr-btn-outline"
            style={{ marginLeft: "auto" }}
            onClick={() => recordsQuery.refetch()}
          >
            <RefreshCw size={13} /> {t("retry")}
          </button>
        </div>
      ) : records.length === 0 ? (
        <div className="spr-empty">
          <FileText size={40} />
          <h3>{t("no_profile_records_yet")}</h3>
          <p>{t("add_first_record_hint")}</p>
          <button
            className="spr-btn spr-btn-primary"
            onClick={openCreate}
            disabled={!canAdd}
            title={deniedTitle(canAdd, t("insert"))}
          >
            <Plus size={13} /> {t("add_record")}
          </button>
        </div>
      ) : (
        <>
          <div className="sprs-actions-bar">
            <span className="sprs-record-count">
              {t(records.length === 1 ? "records_count" : "records_count_plural", { count: records.length })}
            </span>
            <div style={{ display: "flex", gap: 8 }}>
              <button className="spr-btn spr-btn-outline" onClick={() => recordsQuery.refetch()}>
                <RefreshCw size={13} /> {t("refresh")}
              </button>
              <button
                className="spr-btn spr-btn-primary"
                onClick={openCreate}
                disabled={!canAdd}
                title={deniedTitle(canAdd, t("insert"))}
              >
                <Plus size={13} /> {t("add_record")}
              </button>
            </div>
          </div>
          <div className="spr-records-grid">
            {records.map((r) => (
              <div key={r.id} className={`spr-record-card ${r.isSensitive ? "is-sensitive" : ""}`}>
                <div className="spr-record-head">
                  <h3>
                    {r.isSensitive ? <Lock size={13} /> : <FileText size={13} />}
                    {studentProfileService.getProfileCategoryLabel(r.category)}
                    {r.customCategoryKey && (
                      <span style={{ fontWeight: 400, color: "#6b7280", fontSize: 11 }}>
                        ({r.customCategoryKey})
                      </span>
                    )}
                  </h3>
                  <div className="spr-record-actions">
                    {!r.verifiedAt && (
                      <button
                        className="spr-action-btn verify"
                        onClick={() => canModify && handleVerify(r)}
                        disabled={!canModify || verifyMutation.isPending}
                        title={deniedTitle(canModify, t("edit")) || t("verify")}
                      >
                        <ShieldCheck size={13} />
                      </button>
                    )}
                    <button
                      className="spr-action-btn edit"
                      onClick={() => canModify && openEdit(r)}
                      disabled={!canModify}
                      title={deniedTitle(canModify, t("edit")) || t("edit")}
                    >
                      <Edit2 size={13} />
                    </button>
                    <button
                      className="spr-action-btn delete"
                      onClick={() => canDelete && setDeleteTarget(r)}
                      disabled={!canDelete}
                      title={deniedTitle(canDelete, t("delete")) || t("delete")}
                    >
                      <Trash2 size={13} />
                    </button>
                  </div>
                </div>

                <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                  <span className={`spr-badge ${r.verifiedAt ? "spr-badge-verified" : "spr-badge-unverified"}`}>
                    {r.verifiedAt ? t("verified") : t("unverified")}
                  </span>
                  {r.isSensitive && <span className="spr-badge spr-badge-sensitive">{t("sensitive")}</span>}
                  <span className="spr-badge spr-badge-unverified">v{r.schemaVersion}</span>
                </div>

                <pre>{prettifyJson(r.dataJson)}</pre>

                <div className="spr-record-meta">
                  {t("created_date", { date: new Date(r.createdAt).toLocaleDateString() })}
                  {r.updatedAt && r.updatedAt !== r.createdAt && (
                    <> · {t("updated_date", { date: new Date(r.updatedAt).toLocaleDateString() })}</>
                  )}
                </div>
              </div>
            ))}
          </div>
        </>
      )}

      {modalMode && (
        <ProfileRecordFormModal
          mode={modalMode}
          record={editRecord}
          pending={upsertMutation.isPending}
          serverError={serverError}
          onSubmit={(payload) => upsertMutation.mutate(payload)}
          onClose={() => { setModalMode(null); setEditRecord(null); setServerError(""); }}
        />
      )}

      <ConfirmDialog
        open={!!deleteTarget}
        onClose={() => { if (!deleteMutation.isPending) setDeleteTarget(null); }}
        onConfirm={() => deleteMutation.mutate(deleteTarget)}
        title={t("delete_profile_record")}
        message={deleteTarget ? t("delete_record_confirm", { category: studentProfileService.getProfileCategoryLabel(deleteTarget.category) }) : ""}
        detail={t("cannot_undo")}
        confirmLabel={t("delete")}
        cancelLabel={t("cancel")}
        variant="danger"
        loading={deleteMutation.isPending}
      />
    </>
  );
}

export default ProfileRecordsPanel;
