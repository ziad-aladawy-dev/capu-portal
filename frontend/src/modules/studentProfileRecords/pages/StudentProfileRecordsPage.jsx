import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import {
  FileText, ArrowLeft, Plus, Edit2, Trash2, X, AlertTriangle, ShieldCheck,
  RefreshCw, Lock,
} from "lucide-react";
import * as studentProfileService from "../../../core/services/studentProfileService";
import * as studentService from "../../../core/services/studentService";
import { useAuth } from "../../../core/auth/useAuth";
import "../styles/studentProfileRecords.css";

const EMPTY_FORM = {
  category: 1,
  customCategoryKey: "",
  schemaVersion: 1,
  dataJson: "{\n  \n}",
  isSensitive: false,
};

function prettifyJson(value) {
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function StudentProfileRecordsPage() {
  const { t } = useTranslation();
  const { studentId } = useParams();
  const navigate = useNavigate();
  const { user } = useAuth();

  const [student, setStudent] = useState(null);
  const [records, setRecords] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [modalMode, setModalMode] = useState(null); // 'create' | 'edit'
  const [editRecord, setEditRecord] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);
  const [formError, setFormError] = useState("");
  const [saving, setSaving] = useState(false);

  const [deleteTarget, setDeleteTarget] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [studentData, recordsData] = await Promise.all([
        studentService.fetchStudentById(studentId).catch(() => null),
        studentProfileService.fetchProfileRecords(studentId),
      ]);
      setStudent(studentData);
      setRecords(Array.isArray(recordsData) ? recordsData : []);
    } catch (err) {
      setError(err.message || "Failed to load profile records");
      setRecords([]);
    } finally {
      setLoading(false);
    }
  }, [studentId]);

  useEffect(() => {
    load();
  }, [load]);

  const openCreate = () => {
    setModalMode("create");
    setEditRecord(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const openEdit = (record) => {
    setModalMode("edit");
    setEditRecord(record);
    setForm({
      category: record.category,
      customCategoryKey: record.customCategoryKey || "",
      schemaVersion: record.schemaVersion || 1,
      dataJson: prettifyJson(record.dataJson),
      isSensitive: record.isSensitive,
    });
    setFormError("");
  };

  const closeModal = () => {
    setModalMode(null);
    setEditRecord(null);
    setForm(EMPTY_FORM);
    setFormError("");
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    let parsed;
    try {
      parsed = JSON.stringify(JSON.parse(form.dataJson || "{}"));
    } catch {
      setFormError("Data payload must be valid JSON.");
      return;
    }
    if (Number(form.category) === studentProfileService.STUDENT_PROFILE_CATEGORY.Custom
        && !form.customCategoryKey.trim()) {
      setFormError("Custom category key is required for the Custom category.");
      return;
    }
    setSaving(true);
    try {
      await studentProfileService.upsertProfileRecord(studentId, {
        category: Number(form.category),
        customCategoryKey: form.customCategoryKey.trim() || null,
        schemaVersion: Number(form.schemaVersion) || 1,
        dataJson: parsed,
        isSensitive: !!form.isSensitive,
      });
      closeModal();
      await load();
    } catch (err) {
      setFormError(err.message || "Failed to save record");
    } finally {
      setSaving(false);
    }
  };

  const handleVerify = async (record) => {
    if (!user?.id) {
      setError("Cannot verify — current user id unknown.");
      return;
    }
    try {
      await studentProfileService.verifyProfileRecord(studentId, record.id, user.id);
      await load();
    } catch (err) {
      setError(err.message || "Failed to verify record");
    }
  };

  const handleDelete = async () => {
    if (!deleteTarget) return;
    try {
      await studentProfileService.deleteProfileRecord(studentId, deleteTarget.id);
      setDeleteTarget(null);
      await load();
    } catch (err) {
      setError(err.message || "Failed to delete record");
      setDeleteTarget(null);
    }
  };

  return (
    <div className="spr-page">
      <button className="spr-back" onClick={() => navigate(-1)}>
        <ArrowLeft size={14} /> {t("back")}
      </button>

      <div className="spr-header">
        <div className="spr-header-left">
          <FileText size={22} />
          <div>
            <h1>{t("profile_records")}</h1>
            <p>
              {student ? <>{t("for_student")} <strong>{student.name || student.fullNameEn}</strong>{student.studentCode && <> · {student.studentCode}</>}</> : t("student_id_short", { id: studentId.slice(0, 8) })}
            </p>
          </div>
        </div>
        <div style={{ display: "flex", gap: 8 }}>
          <button className="spr-btn spr-btn-outline" onClick={load}>
            <RefreshCw size={13} /> {t("refresh")}
          </button>
          <button className="spr-btn spr-btn-primary" onClick={openCreate}>
            <Plus size={13} /> {t("add_record")}
          </button>
        </div>
      </div>

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

      {loading ? (
        <div className="spr-loading">
          <div className="spr-spinner" />
          <p>{t("loading_profile_records")}</p>
        </div>
      ) : records.length === 0 ? (
        <div className="spr-empty">
          <FileText size={40} />
          <h3>{t("no_profile_records_yet")}</h3>
          <p>{t("add_first_record_hint")}</p>
          <button className="spr-btn spr-btn-primary" onClick={openCreate}>
            <Plus size={13} /> {t("add_record")}
          </button>
        </div>
      ) : (
        <div className="spr-records-grid">
          {records.map((r) => (
            <div
              key={r.id}
              className={`spr-record-card ${r.isSensitive ? "is-sensitive" : ""}`}
            >
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
                      onClick={() => handleVerify(r)}
                      title={t("verify")}
                    >
                      <ShieldCheck size={13} />
                    </button>
                  )}
                  <button
                    className="spr-action-btn edit"
                    onClick={() => openEdit(r)}
                    title={t("edit")}
                  >
                    <Edit2 size={13} />
                  </button>
                  <button
                    className="spr-action-btn delete"
                    onClick={() => setDeleteTarget(r)}
                    title={t("delete")}
                  >
                    <Trash2 size={13} />
                  </button>
                </div>
              </div>

              <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
                <span
                  className={`spr-badge ${r.verifiedAt ? "spr-badge-verified" : "spr-badge-unverified"}`}
                >
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
      )}

      {modalMode && (
        <div className="spr-modal-overlay" onClick={closeModal}>
          <div className="spr-modal" onClick={(e) => e.stopPropagation()}>
            <div className="spr-modal-header">
              <h2>{modalMode === "create" ? t("add_profile_record") : t("edit_profile_record")}</h2>
              <button className="spr-modal-close" onClick={closeModal}>
                <X size={16} />
              </button>
            </div>
            <form onSubmit={handleSubmit}>
              <div className="spr-modal-body">
                <div className="spr-form-row">
                  <div className="spr-form-group">
                    <label>{t("category")}</label>
                    <select
                      className="spr-form-select"
                      value={form.category}
                      onChange={(e) =>
                        setForm((p) => ({ ...p, category: Number(e.target.value) }))
                      }
                      disabled={modalMode === "edit"}
                    >
                      {studentProfileService.STUDENT_PROFILE_CATEGORY_OPTIONS.map((o) => (
                        <option key={o.value} value={o.value}>
                          {o.label}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div className="spr-form-group">
                    <label>{t("schema_version")}</label>
                    <input
                      type="number"
                      className="spr-form-input"
                      value={form.schemaVersion}
                      min={1}
                      onChange={(e) =>
                        setForm((p) => ({ ...p, schemaVersion: e.target.value }))
                      }
                    />
                  </div>
                </div>

                {Number(form.category) === studentProfileService.STUDENT_PROFILE_CATEGORY.Custom && (
                  <div className="spr-form-group">
                    <label>{t("custom_category_key")}</label>
                    <input
                      type="text"
                      className="spr-form-input"
                      value={form.customCategoryKey}
                      onChange={(e) =>
                        setForm((p) => ({ ...p, customCategoryKey: e.target.value }))
                      }
                      placeholder={t("custom_category_key_placeholder")}
                      disabled={modalMode === "edit"}
                    />
                  </div>
                )}

                <div className="spr-form-group">
                  <label>{t("data_payload_json")}</label>
                  <textarea
                    className="spr-form-textarea"
                    rows={9}
                    value={form.dataJson}
                    onChange={(e) =>
                      setForm((p) => ({ ...p, dataJson: e.target.value }))
                    }
                  />
                  <span style={{ fontSize: 11, color: "#6b7280" }}>
                    {t("schema_category_specific")}
                  </span>
                </div>

                <label className="spr-checkbox-row">
                  <input
                    type="checkbox"
                    checked={form.isSensitive}
                    onChange={(e) =>
                      setForm((p) => ({ ...p, isSensitive: e.target.checked }))
                    }
                  />
                  {t("mark_as_sensitive")}
                </label>

                {formError && <span className="spr-form-error">{formError}</span>}
              </div>
              <div className="spr-modal-footer">
                <button
                  type="button"
                  className="spr-btn spr-btn-outline"
                  onClick={closeModal}
                  disabled={saving}
                >
                  {t("cancel")}
                </button>
                <button
                  type="submit"
                  className="spr-btn spr-btn-primary"
                  disabled={saving}
                >
                  {saving ? t("saving") : modalMode === "create" ? t("create") : t("save")}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {deleteTarget && (
        <div className="spr-modal-overlay" onClick={() => setDeleteTarget(null)}>
          <div className="spr-modal" onClick={(e) => e.stopPropagation()}>
            <div className="spr-modal-header">
              <h2>{t("delete_profile_record")}</h2>
              <button className="spr-modal-close" onClick={() => setDeleteTarget(null)}>
                <X size={16} />
              </button>
            </div>
            <div style={{ padding: "20px 22px", textAlign: "center", display: "flex", flexDirection: "column", alignItems: "center", gap: 10 }}>
              <AlertTriangle size={32} color="#dc2626" />
              <p style={{ margin: 0 }}>
                {t("delete_record_confirm", { category: studentProfileService.getProfileCategoryLabel(deleteTarget.category) })}
              </p>
              <p style={{ margin: 0, fontSize: 12, color: "#6b7280" }}>
                {t("cannot_undo")}
              </p>
            </div>
            <div className="spr-modal-footer">
              <button
                className="spr-btn spr-btn-outline"
                onClick={() => setDeleteTarget(null)}
              >
                {t("cancel")}
              </button>
              <button className="spr-btn spr-btn-danger" onClick={handleDelete}>
                {t("delete")}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default StudentProfileRecordsPage;
