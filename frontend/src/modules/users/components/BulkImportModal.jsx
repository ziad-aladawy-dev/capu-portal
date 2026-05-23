import { useState, useRef } from "react";
import { Upload, FileText, X, AlertCircle, CheckCircle, Download, Loader } from "lucide-react";
import userService from "../services/userService";
import { useDomain } from "../../../core/contexts/DomainContext";
import { useAcademic } from "../../../core/contexts/AcademicContext";
import "../styles/users.css";

function BulkImportModal({ userType = "students", onClose, onSuccess }) {
  const { scopeNode } = useDomain();
  const { selectedYearObj, selectedSemesterObj } = useAcademic();
  const [file, setFile] = useState(null);
  const [importFormat, setImportFormat] = useState("excel");
  const [importing, setImporting] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [dragOver, setDragOver] = useState(false);
  const fileInputRef = useRef(null);

  const handleDragOver = (e) => {
    e.preventDefault();
    setDragOver(true);
  };

  const handleDragLeave = () => setDragOver(false);

  const handleDrop = (e) => {
    e.preventDefault();
    setDragOver(false);
    const droppedFile = e.dataTransfer.files[0];
    if (droppedFile) validateAndSetFile(droppedFile);
  };

  const handleFileSelect = (e) => {
    const selectedFile = e.target.files[0];
    if (selectedFile) validateAndSetFile(selectedFile);
  };

  const validateAndSetFile = (f) => {
    const validTypes = [".xlsx", ".xls", ".csv"];
    const ext = "." + f.name.split(".").pop().toLowerCase();
    if (!validTypes.includes(ext)) {
      setError("Invalid file type. Please upload an Excel (.xlsx, .xls) or CSV file.");
      setFile(null);
      return;
    }
    setError(null);
    setFile(f);
    setResult(null);
    setImportFormat(ext === ".csv" ? "csv" : "excel");
  };

  const getImportFunc = () => {
    if (userType === "students") {
      return importFormat === "csv" ? userService.importStudentsCsv : userService.importStudentsExcel;
    }
    return importFormat === "csv" ? userService.importStaffCsv : userService.importStaffExcel;
  };

  const handleImport = async () => {
    if (!file) return;
    setImporting(true);
    setError(null);
    setResult(null);
    try {
      const importFunc = getImportFunc();
      const response = await importFunc(file, scopeNode?.id, selectedYearObj?.id, selectedSemesterObj?.id);
      setResult(response);
      if (onSuccess) onSuccess();
    } catch (err) {
      setError(err.message || "Import failed. Please check your file and try again.");
    } finally {
      setImporting(false);
    }
  };

  const formatFileSize = (bytes) => {
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KB";
    return (bytes / (1024 * 1024)).toFixed(1) + " MB";
  };

  return (
    <div className="modal-overlay" onClick={() => !importing && onClose()}>
      <div className="modal-content import-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <div className="modal-header-left">
            <Upload size={20} />
            <div>
              <h3>Bulk Import {userType === "students" ? "Students" : "Staff"}</h3>
              <p className="modal-subtitle">
                Upload an Excel or CSV file to import multiple {userType === "students" ? "students" : "staff members"} at once
              </p>
            </div>
          </div>
          <button className="modal-close-btn" onClick={onClose} disabled={importing}>
            <X size={20} />
          </button>
        </div>

        <div className="import-body">
          {result ? (
            <div className="import-result">
              <div className="result-icon success">
                <CheckCircle size={48} />
              </div>
              <h3>Import Complete</h3>
              <div className="result-stats">
                <div className="result-stat">
                  <span className="stat-num success">{result.imported || result.successCount || 0}</span>
                  <span>Imported</span>
                </div>
                <div className="result-stat">
                  <span className="stat-num warning">{result.skipped || result.skippedCount || 0}</span>
                  <span>Skipped</span>
                </div>
                <div className="result-stat">
                  <span className="stat-num error">{result.failed || result.errorCount || 0}</span>
                  <span>Failed</span>
                </div>
              </div>
              <button className="btn-primary" onClick={onClose}>
                Done
              </button>
            </div>
          ) : (
            <>
              <div className="import-info">
                <FileText size={16} />
                <span>Supported formats: .xlsx, .xls, .csv</span>
              </div>

              <div
                className={`drop-zone ${dragOver ? "drag-over" : ""} ${file ? "has-file" : ""}`}
                onDragOver={handleDragOver}
                onDragLeave={handleDragLeave}
                onDrop={handleDrop}
                onClick={() => fileInputRef.current?.click()}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept=".xlsx,.xls,.csv"
                  onChange={handleFileSelect}
                  style={{ display: "none" }}
                />
                {file ? (
                  <div className="file-info">
                    <FileText size={24} />
                    <div>
                      <strong>{file.name}</strong>
                      <p>{formatFileSize(file.size)}</p>
                    </div>
                    <button
                      className="file-remove"
                      onClick={(e) => {
                        e.stopPropagation();
                        setFile(null);
                        setError(null);
                      }}
                      disabled={importing}
                    >
                      <X size={16} />
                    </button>
                  </div>
                ) : (
                  <>
                    <Upload size={36} className="drop-icon" />
                    <h3>Drop file here or click to browse</h3>
                    <p>Drag and drop your Excel or CSV file</p>
                  </>
                )}
              </div>

              {error && (
                <div className="alert alert-error">
                  <AlertCircle size={16} />
                  {error}
                </div>
              )}

              <div className="import-template">
                <h4>Need a template?</h4>
                <a href="#" className="template-link">
                  <Download size={14} />
                  Download import template for {userType === "students" ? "students" : "staff"}
                </a>
              </div>
            </>
          )}
        </div>

        {!result && (
          <div className="modal-actions">
            <button className="btn-cancel" onClick={onClose} disabled={importing}>
              Cancel
            </button>
            <button
              className="btn-primary"
              onClick={handleImport}
              disabled={!file || importing}
            >
              {importing ? (
                <>
                  <Loader size={16} className="spin" />
                  Importing...
                </>
              ) : (
                <>
                  <Upload size={16} />
                  Import {userType === "students" ? "Students" : "Staff"}
                </>
              )}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

export default BulkImportModal;
