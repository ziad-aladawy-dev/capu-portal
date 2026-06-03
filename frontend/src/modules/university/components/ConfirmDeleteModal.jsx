import { useTranslation } from "react-i18next";
import { X } from "lucide-react";

export function ConfirmDeleteModal({ isOpen, onClose, onConfirm, nodeName }) {
  const { t } = useTranslation();

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-container confirm-modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{t("delete_node")}</h3>
          <button className="modal-close" onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        <div className="modal-body">
          <p>
            {t("confirm_delete_message", { name: nodeName })}
          </p>
        </div>
        <div className="modal-footer">
          <button className="btn-secondary" onClick={onClose}>
            {t("cancel")}
          </button>
          <button className="btn-danger" onClick={onConfirm}>
            {t("delete")}
          </button>
        </div>
      </div>
    </div>
  );
}