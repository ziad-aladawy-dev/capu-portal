import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { X } from "lucide-react";
import { useTranslation } from "react-i18next";
import "../styles/roles.css";

// Create/rename role modal — react-hook-form + zod with inline validation.
function RoleFormModal({ title, initialName = "", submitLabel, pending, serverError, onSubmit, onClose }) {
  const { t } = useTranslation();

  const schema = z.object({
    name: z.string()
      .trim()
      .min(1, t("role_name_required"))
      .min(2, t("role_name_min_length"))
      .max(100, t("role_name_max_length")),
  });

  const {
    register, handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { name: initialName },
    mode: "onChange",
  });

  const busy = pending || isSubmitting;
  const fieldError = errors.name?.message || serverError;

  return (
    <div className="roles-modal-overlay" onClick={() => !busy && onClose()}>
      <div className="roles-modal" onClick={(e) => e.stopPropagation()}>
        <div className="roles-modal-header">
          <h2>{title}</h2>
          <button className="roles-modal-close" onClick={onClose} disabled={busy}><X size={16} /></button>
        </div>
        <form onSubmit={handleSubmit((values) => onSubmit(values.name.trim()))}>
          <div className="roles-modal-body">
            <div className="roles-form-group">
              <label htmlFor="role-name">{t("role_name")}</label>
              <input
                id="role-name"
                type="text"
                className={`roles-form-input ${fieldError ? "error" : ""}`}
                placeholder={t("role_name_example")}
                autoFocus
                maxLength={100}
                {...register("name")}
              />
              {fieldError && <span className="roles-form-error">{fieldError}</span>}
              <span className="roles-form-hint">{t("role_name_hint")}</span>
            </div>
          </div>
          <div className="roles-modal-footer">
            <button type="button" className="btn-outline" onClick={onClose} disabled={busy}>{t("cancel")}</button>
            <button type="submit" className="btn-primary" disabled={busy}>
              {busy ? t("saving") : submitLabel}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default RoleFormModal;
