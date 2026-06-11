import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslation } from "react-i18next";
import Drawer from "../../../../core/components/Drawer";
import { toLocalizedJson } from "../../../../core/utils/getLocalized";
import shared from "../../styles/academic.module.css";

const EMPTY_FORM = { code: "", title: "", titleAr: "", creditHours: 3, category: 0 };

/**
 * Create/Edit course drawer on react-hook-form + zod. The API stores titles as
 * {"ar","en"} localized JSON; on edit, title is only resent when actually
 * changed so the other language is never clobbered.
 */
export default function CourseFormDrawer({ open, mode, course, onClose, onSubmit, saving, serverError }) {
  const { t } = useTranslation("academic");

  const schema = z.object({
    code: z.string().trim().min(2, t("courses.validation.codeRequired")).max(32, t("courses.validation.codeRequired"))
      .transform((s) => s.toUpperCase()),
    title: z.string().trim().min(1, t("courses.validation.titleRequired")).max(200),
    titleAr: z.string().trim().max(200).optional().or(z.literal("")),
    creditHours: z.coerce.number().int().min(0, t("courses.validation.creditRange")).max(30, t("courses.validation.creditRange")),
    category: z.coerce.number().int().min(0).max(5),
  });

  const { register, handleSubmit, formState: { errors }, reset } = useForm({
    resolver: zodResolver(schema),
    defaultValues: EMPTY_FORM,
  });

  useEffect(() => {
    if (!open) return;
    if (mode === "edit" && course) {
      reset({
        code: course.code || "",
        title: course.title || "",
        titleAr: "",
        creditHours: course.creditHours ?? 0,
        category: course.category ?? 0,
      });
    } else {
      reset(EMPTY_FORM);
    }
  }, [open, mode, course, reset]);

  const submit = handleSubmit((values) => {
    if (mode === "create") {
      onSubmit({
        code: values.code,
        title: toLocalizedJson(values.titleAr, values.title),
        creditHours: values.creditHours,
        category: values.category,
      });
    } else {
      const payload = { creditHours: values.creditHours, category: values.category };
      if (values.title !== (course?.title || "") || values.titleAr) {
        payload.title = toLocalizedJson(values.titleAr, values.title);
      }
      onSubmit(payload);
    }
  });

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={mode === "create" ? t("courses.createCourse") : t("courses.editCourse")}
      width={440}
      loading={saving}
      footer={
        <>
          <button className="btn-cancel" onClick={onClose} disabled={saving}>{t("common.cancel")}</button>
          <button className="btn-primary" onClick={submit} disabled={saving}>
            {saving ? t("common.saving") : mode === "create" ? t("common.create") : t("common.save")}
          </button>
        </>
      }
    >
      {serverError && (
        <div className={shared.errorBanner} role="alert">{serverError}</div>
      )}

      <form onSubmit={submit}>
        <div className={shared.formGroup}>
          <label htmlFor="ac-course-code">{t("courses.code")}</label>
          <input
            id="ac-course-code"
            type="text"
            className={shared.formInput}
            placeholder="e.g. CS101"
            maxLength={32}
            disabled={mode === "edit"}
            autoFocus={mode === "create"}
            {...register("code")}
          />
          {mode === "edit" && <span className={shared.formHint}>{t("courses.codeHint")}</span>}
          {errors.code && <span className={shared.formError}>{errors.code.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="ac-course-title">{t("courses.courseTitle")}</label>
          <input
            id="ac-course-title"
            type="text"
            className={shared.formInput}
            placeholder="e.g. Introduction to Computer Science"
            maxLength={200}
            {...register("title")}
          />
          {errors.title && <span className={shared.formError}>{errors.title.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="ac-course-title-ar">{t("courses.titleAr")}</label>
          <input
            id="ac-course-title-ar"
            type="text"
            dir="rtl"
            className={shared.formInput}
            placeholder="مثال: مقدمة في علوم الحاسب"
            maxLength={200}
            {...register("titleAr")}
          />
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="ac-course-credits">{t("courses.creditHours")}</label>
          <input
            id="ac-course-credits"
            type="number"
            className={shared.formInput}
            min={0}
            max={30}
            {...register("creditHours")}
          />
          {errors.creditHours && <span className={shared.formError}>{errors.creditHours.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="ac-course-category">{t("courses.category")}</label>
          <select id="ac-course-category" className={shared.formInput} {...register("category")}>
            {[0, 1, 2, 3, 4, 5].map((v) => (
              <option key={v} value={v}>{t(`categories.${v}`)}</option>
            ))}
          </select>
        </div>

        {/* Allow Enter-to-submit */}
        <button type="submit" style={{ display: "none" }} aria-hidden="true" />
      </form>
    </Drawer>
  );
}
