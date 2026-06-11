import React, { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Upload, X, File, Loader2 } from "lucide-react";
import { useFileUpload } from "../hooks/useFileUpload";
import "../styles/components/FileUploader.css";

const FileUploader = ({ requestId, stepKey, value = [], onChange }) => {
  const { t } = useTranslation();
  const { upload, uploading, error } = useFileUpload();
  const fileInputRef = useRef(null);
  const [uploadingIds, setUploadingIds] = useState([]);

  const safeValue = Array.isArray(value) ? value : [];

  const handleFileSelect = async (e) => {
    const files = Array.from(e.target.files);
    const newFiles = [...safeValue];
    
    for (const file of files) {
      const tempId = Date.now() + Math.random();
      const newFile = { 
        id: tempId, 
        name: file.name, 
        size: file.size, 
        file, 
        uploading: true,
        uploaded: false
      };
      newFiles.push(newFile);
      onChange(newFiles);
      setUploadingIds(prev => [...prev, tempId]);
      
      try {
        const result = await upload(requestId, stepKey, file);
        const finalFile = { 
          id: result.attachmentId, 
          name: file.name, 
          size: file.size, 
          attachmentId: result.attachmentId, 
          uploaded: true,
          uploading: false
        };
        const updatedFiles = newFiles.map(f => f.id === tempId ? finalFile : f);
        onChange(updatedFiles);
      } catch (err) {
        const updatedFiles = newFiles.filter(f => f.id !== tempId);
        onChange(updatedFiles);
      } finally {
        setUploadingIds(prev => prev.filter(id => id !== tempId));
      }
    }
    if (fileInputRef.current) fileInputRef.current.value = "";
  };

  const removeFile = (file) => {
    const updated = safeValue.filter(f => f.id !== file.id);
    onChange(updated);
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
        <Upload size={32} /><p>{t("drag_drop_or_click")}</p>
        <input type="file" multiple ref={fileInputRef} onChange={handleFileSelect} style={{ display: "none" }} disabled={uploading} />
      </div>
      {error && <div className="fu-error">{error}</div>}
      {safeValue.length > 0 && (
        <div className="fu-list">
          {safeValue.map(file => (
            <div key={file.id} className="fu-item">
              {isUploading(file) ? <Loader2 size={18} className="fu-spinner" /> : <File size={18} />}
              <div className="fu-file-info">
                <span className="fu-file-name">{file.name}</span>
                <span className="fu-file-size">{formatFileSize(file.size)}</span>
              </div>
              <button onClick={() => removeFile(file)} className="fu-remove-btn" disabled={isUploading(file)}>
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