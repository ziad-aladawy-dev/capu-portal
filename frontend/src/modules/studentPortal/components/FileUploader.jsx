import React, { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Upload, X, File, Loader2 } from "lucide-react";
import { useFileUpload } from "../hooks/useFileUpload";
import "../../studentServices/styles/components/FileUploader.css";

const FileUploader = ({ requestId, stepKey, value = [], onChange }) => {
  const { t } = useTranslation();
  const { upload, uploading, error } = useFileUpload();
  const fileInputRef = useRef(null);
  const [uploadingIds, setUploadingIds] = useState([]);

  const handleFileSelect = async (e) => {
    const files = Array.from(e.target.files);
    const newFiles = [];
    for (const file of files) {
      const tempId = Date.now() + Math.random();
      newFiles.push({ id: tempId, name: file.name, size: file.size, file, uploading: true });
      setUploadingIds((prev) => [...prev, tempId]);
      onChange([...value, ...newFiles.filter(f => f.id === tempId)]);
      try {
        const result = await upload(requestId, stepKey, file);
        const finalFile = {
          id: result.attachmentId,
          name: file.name,
          size: file.size,
          attachmentId: result.attachmentId,
          uploaded: true,
        };
        onChange((prev) => prev.map(f => f.id === tempId ? finalFile : f));
      } catch (err) {
        onChange((prev) => prev.filter(f => f.id !== tempId));
      } finally {
        setUploadingIds((prev) => prev.filter(id => id !== tempId));
      }
    }
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const removeFile = async (file) => {
    if (file.attachmentId) {
      await remove(file.attachmentId);
    }
    onChange(value.filter(f => f.id !== file.id));
  };

  const formatFileSize = (bytes) => {
    if (!bytes) return "";
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  };

  const isUploading = (file) => uploadingIds.includes(file.id);

  return (
    <div className="fu-container">
      <div className="fu-dropzone" onClick={() => fileInputRef.current?.click()}>
        <Upload size={32} />
        <p>{t("drag_drop_or_click")}</p>
        <input
          type="file"
          multiple
          ref={fileInputRef}
          onChange={handleFileSelect}
          style={{ display: "none" }}
          disabled={uploading}
        />
      </div>
      {error && <div className="fu-error">{error}</div>}
      {value.length > 0 && (
        <div className="fu-list">
          {value.map((file) => (
            <div key={file.id} className="fu-item">
              {isUploading(file) ? (
                <Loader2 size={18} className="fu-spinner" />
              ) : (
                <File size={18} />
              )}
              <div className="fu-file-info">
                <span className="fu-file-name">{file.name}</span>
                <span className="fu-file-size">{formatFileSize(file.size)}</span>
              </div>
              <button
                onClick={() => removeFile(file)}
                className="fu-remove-btn"
                disabled={isUploading(file)}
              >
                <X size={14} />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default FileUploader;