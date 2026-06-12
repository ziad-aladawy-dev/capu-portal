import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Upload, X, File, Loader2, AlertCircle, RotateCcw } from "lucide-react";
import { useFileUpload } from "../hooks/useFileUpload";
import styles from "./FileUploader.module.css";

/**
 * Upload-on-select file list for one workflow File field.
 *
 * Contract: `onChange` always receives the NEXT ARRAY (never a functional
 * updater) — parents may store the argument verbatim. A ref mirrors the
 * latest list so sequential awaited uploads never act on a stale closure.
 */
function FileUploader({ requestId, stepKey, value = [], onChange, disabled = false }) {
  const { t } = useTranslation();
  const { upload, remove } = useFileUpload();
  const fileInputRef = useRef(null);

  const listRef = useRef(value);
  useEffect(() => { listRef.current = value; }, [value]);

  const commit = (next) => {
    listRef.current = next;
    onChange(next);
  };

  const uploadOne = async (entry) => {
    commit(listRef.current.map((f) => (f.id === entry.id ? { ...f, uploading: true, error: null } : f)));
    try {
      const result = await upload(requestId, stepKey, entry.file);
      commit(listRef.current.map((f) =>
        f.id === entry.id
          ? { ...f, id: result.attachmentId, attachmentId: result.attachmentId, uploading: false, uploaded: true, error: null }
          : f
      ));
    } catch (err) {
      // Keep the row visible with an explicit error — no silent removal.
      commit(listRef.current.map((f) =>
        f.id === entry.id
          ? { ...f, uploading: false, uploaded: false, error: err.message || t("portal_requests.upload_failed", { defaultValue: "Upload failed" }) }
          : f
      ));
    }
  };

  const handleFileSelect = async (e) => {
    const files = Array.from(e.target.files || []);
    if (fileInputRef.current) fileInputRef.current.value = "";
    if (!files.length || !requestId) return;

    const entries = files.map((file) => ({
      id: `tmp-${Date.now()}-${Math.random().toString(36).slice(2)}`,
      name: file.name,
      size: file.size,
      file,
      uploading: true,
      uploaded: false,
      error: null,
    }));
    commit([...listRef.current, ...entries]);

    for (const entry of entries) {
      await uploadOne(entry);
    }
  };

  const removeFile = async (file) => {
    if (file.attachmentId) {
      try {
        await remove(file.attachmentId);
      } catch {
        // Server copy may already be gone; still drop it locally.
      }
    }
    commit(listRef.current.filter((f) => f.id !== file.id));
  };

  const formatFileSize = (bytes) => {
    if (!bytes) return "";
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const canPick = Boolean(requestId) && !disabled;

  return (
    <div className={styles.container}>
      <button
        type="button"
        className={styles.dropzone}
        onClick={() => canPick && fileInputRef.current?.click()}
        disabled={!canPick}
      >
        <Upload size={26} />
        <span>{t("portal_requests.upload_click", { defaultValue: "Click to choose files" })}</span>
      </button>
      <input
        type="file"
        multiple
        ref={fileInputRef}
        onChange={handleFileSelect}
        className={styles.hiddenInput}
        disabled={!canPick}
      />

      {value.length > 0 && (
        <ul className={styles.list}>
          {value.map((file) => (
            <li key={file.id} className={`${styles.item} ${file.error ? styles.itemError : ""}`}>
              <span className={styles.itemIcon}>
                {file.uploading ? (
                  <Loader2 size={16} className={styles.spinner} />
                ) : file.error ? (
                  <AlertCircle size={16} />
                ) : (
                  <File size={16} />
                )}
              </span>
              <span className={styles.fileInfo}>
                <span className={styles.fileName}>{file.name}</span>
                <span className={styles.fileMeta}>
                  {file.uploading
                    ? t("portal_requests.uploading", { defaultValue: "Uploading…" })
                    : file.error
                      ? file.error
                      : formatFileSize(file.size)}
                </span>
              </span>
              {file.error && file.file && (
                <button
                  type="button"
                  className={styles.retryBtn}
                  onClick={() => uploadOne(file)}
                  title={t("portal_requests.retry_upload", { defaultValue: "Retry" })}
                >
                  <RotateCcw size={13} />
                </button>
              )}
              <button
                type="button"
                className={styles.removeBtn}
                onClick={() => removeFile(file)}
                disabled={file.uploading}
                title={t("portal_requests.remove_file", { defaultValue: "Remove" })}
              >
                <X size={13} />
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

export default FileUploader;
