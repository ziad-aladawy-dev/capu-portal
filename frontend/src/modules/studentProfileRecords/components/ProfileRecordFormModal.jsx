import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { X } from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  STUDENT_PROFILE_CATEGORY, STUDENT_PROFILE_CATEGORY_OPTIONS,
} from "../../../core/services/studentProfileService";
import { prettifyJson } from "./recordUtils";

// Create/edit form for a profile record — react-hook-form + zod with inline
// validation (JSON payload syntax, custom-category key requirement).
function ProfileRecordFormModal({ mode, record, pending, serverError, onSubmit, onClose }) {
  const { t } = useTranslation();

  const schema = z.object({
    category: z.coerce.number(),
    schemaVersion: z.coerce.number().int().min(1, t("schema_version_min", { defaultValue: "Version must be at least 1" })),
    customCategoryKey: z.string(),
    dataJson: z.string().refine((val) => {
      try { JSON.parse(val || "{}"); return true; } catch { return false; }
    }, t("invalid_json_payload", { defaultValue: "Data payload must be valid JSON." })),
    isSensitive: z.boolean(),
  }).superRefine((data, ctx) => {
    if (Number(data.category) === STUDENT_PROFILE_CATEGORY.Custom && !data.customCategoryKey.trim()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["customCategoryKey"],
        message: t("custom_key_required", { defaultValue: "Custom category key is required for the Custom category." }),
      });
    }
  });

  const {
    register, handleSubmit, watch,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    mode: "onChange",
    defaultValues: record ? {
      category: record.category,
      customCategoryKey: record.customCategoryKey || "",
      schemaVersion: record.schemaVersion || 1,
      dataJson: prettifyJson(record.dataJson),
      isSensitive: !!record.isSensitive,
    } : {
      category: 1,
      customCategoryKey: "",
      schemaVersion: 1,
      dataJson: "{\n  \n}",
      isSensitive: false,
    },
  });

  const watchedCategory = watch("category");

  const submit = (values) => {
    onSubmit({
      category: Number(values.category),
      customCategoryKey: values.customCategoryKey.trim() || null,
      schemaVersion: Number(values.schemaVersion) || 1,
      dataJson: JSON.stringify(JSON.parse(values.dataJson || "{}")),
      isSensitive: !!values.isSensitive,
    });
  };

  return (
    <div className="spr-modal-overlay" onClick={() => !pending && onClose()}>
      <div className="spr-modal" onClick={(e) => e.stopPropagation()}>
        <div className="spr-modal-header">
          <h2>{mode === "create" ? t("add_profile_record") : t("edit_profile_record")}</h2>
          <button className="spr-modal-close" onClick={onClose} disabled={pending}>
            <X size={16} />
          </button>
        </div>
        <form onSubmit={handleSubmit(submit)}>
          <div className="spr-modal-body">
            <div className="spr-form-row">
              <div className="spr-form-group">
                <label>{t("category")}</label>
                <select
                  className="spr-form-select"
                  disabled={mode === "edit"}
                  {...register("category")}
                >
                  {STUDENT_PROFILE_CATEGORY_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </div>
              <div className="spr-form-group">
                <label>{t("schema_version")}</label>
                <input
                  type="number"
                  className="spr-form-input"
                  min={1}
                  {...register("schemaVersion")}
                />
                {errors.schemaVersion && <span className="spr-form-error">{errors.schemaVersion.message}</span>}
              </div>
            </div>

            {Number(watchedCategory) === STUDENT_PROFILE_CATEGORY.Custom && (
              <div className="spr-form-group">
                <label>{t("custom_category_key")}</label>
                <input
                  type="text"
                  className="spr-form-input"
                  placeholder={t("custom_category_key_placeholder")}
                  disabled={mode === "edit"}
                  {...register("customCategoryKey")}
                />
                {errors.customCategoryKey && <span className="spr-form-error">{errors.customCategoryKey.message}</span>}
              </div>
            )}

            <div className="spr-form-group">
              <label>{t("data_payload_json")}</label>
              <textarea
                className="spr-form-textarea"
                rows={9}
                {...register("dataJson")}
              />
              {errors.dataJson
                ? <span className="spr-form-error">{errors.dataJson.message}</span>
                : <span style={{ fontSize: 11, color: "#6b7280" }}>{t("schema_category_specific")}</span>}
            </div>

            <label className="spr-checkbox-row">
              <input type="checkbox" {...register("isSensitive")} />
              {t("mark_as_sensitive")}
            </label>

            {serverError && <span className="spr-form-error">{serverError}</span>}
          </div>
          <div className="spr-modal-footer">
            <button type="button" className="spr-btn spr-btn-outline" onClick={onClose} disabled={pending}>
              {t("cancel")}
            </button>
            <button type="submit" className="spr-btn spr-btn-primary" disabled={pending}>
              {pending ? t("saving") : mode === "create" ? t("create") : t("save")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export default ProfileRecordFormModal;
