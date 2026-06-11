import { useState, useCallback } from "react";
import { uploadFile, downloadFile, deleteFile } from "../services/studentServicesService";

export const useFileUpload = () => {
  const [uploading, setUploading] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState(null);

  const upload = useCallback(async (requestId, stepKey, file) => {
    setUploading(true);
    try {
      const result = await uploadFile(requestId, stepKey, file);
      return result;
    } catch (err) { setError(err.message); throw err; }
    finally { setUploading(false); }
  }, []);

  const download = useCallback(async (id, name = "file") => {
    setDownloading(true);
    try {
      const blob = await downloadFile(id);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url; a.download = name;
      document.body.appendChild(a); a.click(); a.remove();
      URL.revokeObjectURL(url);
    } catch (err) { setError(err.message); throw err; }
    finally { setDownloading(false); }
  }, []);

  const remove = useCallback(async (id) => {
    await deleteFile(id);
  }, []);

  return { upload, download, remove, uploading, downloading, error };
};