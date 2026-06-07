import { useState, useCallback } from "react";
import {
  uploadFile,
  downloadFile,
  deleteFile,
} from "../../studentServices/services/studentServicesService";

export const useFileUpload = () => {
  const [uploading, setUploading] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState(null);

  const upload = useCallback(async (requestId, stepKey, file) => {
    setUploading(true);
    setError(null);
    try {
      const result = await uploadFile(requestId, stepKey, file);
      return result;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Upload failed";
      setError(msg);
      throw new Error(msg);
    } finally {
      setUploading(false);
    }
  }, []);

  const download = useCallback(async (attachmentId, fileName = "file") => {
    setDownloading(true);
    setError(null);
    try {
      const blob = await downloadFile(attachmentId);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
      return true;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Download failed";
      setError(msg);
      throw new Error(msg);
    } finally {
      setDownloading(false);
    }
  }, []);

  const remove = useCallback(async (attachmentId) => {
    setError(null);
    try {
      await deleteFile(attachmentId);
      return true;
    } catch (err) {
      const msg = err.response?.data?.message || err.message || "Delete failed";
      setError(msg);
      throw new Error(msg);
    }
  }, []);

  return {
    upload,
    download,
    remove,
    uploading,
    downloading,
    error,
  };
};