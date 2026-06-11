import { useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslation } from "react-i18next";
import { Layers } from "lucide-react";
import Drawer from "../../../../core/components/Drawer";
import { useToast } from "../../../../core/components/Toast";
import { useActiveCourses } from "../../../../core/query/useCourses";
import { useBatchCreateOfferings } from "../../../../core/query/useCourseOfferings";
import { useStaffOptions } from "./useStaffOptions";
import shared from "../../styles/academic.module.css";

const letterSeries = (n) => {
  const out = [];
  for (let i = 0; i < n; i++) {
    let code = "", x = i + 1;
    while (x > 0) { x--; code = String.fromCharCode(65 + (x % 26)) + code; x = Math.floor(x / 26); }
    out.push(code);
  }
  return out;
};

/**
 * "Create Multiple Sections" wizard. Section codes are generated server-side
 * (existing codes are skipped); the preview here shows the nominal series.
 */
export default function BatchSectionsWizard({ open, onClose, semester, scopeNode }) {
  const { t } = useTranslation("academic");
  const { addToast } = useToast();
  const [serverError, setServerError] = useState("");

  const { data: courses = [] } = useActiveCourses();
  const { data: staff = [] } = useStaffOptions();
  const batchCreate = useBatchCreateOfferings();

  const schema = z.object({
    courseId: z.string().min(1, t("schedule.validation.offeringRequired")),
    sectionPrefix: z.string().trim().max(28).optional().or(z.literal("")),
    count: z.coerce.number().int().min(1).max(50),
    capacity: z.coerce.number().int().min(0).max(9999),
    instructorId: z.string().optional().or(z.literal("")),
  });

  const { register, handleSubmit, watch, reset, formState: { errors } } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { courseId: "", sectionPrefix: "", count: 3, capacity: 30, instructorId: "" },
  });

  const prefix = watch("sectionPrefix");
  const count = Number(watch("count")) || 0;

  const preview = useMemo(() => {
    if (count < 1 || count > 50) return "";
    const codes = prefix?.trim()
      ? Array.from({ length: Math.min(count, 8) }, (_, i) => `${prefix.trim()}${i + 1}`)
      : letterSeries(Math.min(count, 8));
    return codes.join(", ") + (count > 8 ? "…" : "");
  }, [prefix, count]);

  const submit = handleSubmit(async (values) => {
    setServerError("");
    try {
      const result = await batchCreate.mutateAsync({
        courseId: values.courseId,
        semesterId: semester.id,
        structureNodeId: scopeNode?.id,
        sectionPrefix: values.sectionPrefix?.trim() || null,
        count: values.count,
        capacity: values.capacity,
        instructorId: values.instructorId || null,
      });
      const failedPart = result.failed > 0 ? t("offerings.batch.failedPart", { failed: result.failed }) : "";
      addToast(
        t("offerings.batch.done", { succeeded: result.succeeded, failedPart }),
        result.failed > 0 ? "warning" : "success"
      );
      reset();
      onClose();
    } catch (err) {
      setServerError(err.message || t("courses.saveFailed"));
    }
  });

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={t("offerings.batch.title")}
      width={460}
      loading={batchCreate.isPending}
      footer={
        <>
          <button className="btn-cancel" onClick={onClose} disabled={batchCreate.isPending}>{t("common.cancel")}</button>
          <button className="btn-primary" onClick={submit} disabled={batchCreate.isPending || !semester}>
            <Layers size={13} /> {batchCreate.isPending ? t("common.saving") : t("offerings.batch.create", { count })}
          </button>
        </>
      }
    >
      {serverError && <div className={shared.errorBanner} role="alert">{serverError}</div>}
      {!semester && <div className={shared.warnBanner}>{t("common.noSemester")}</div>}

      <form onSubmit={submit}>
        <div className={shared.formGroup}>
          <label htmlFor="bw-course">{t("offerings.batch.course")} *</label>
          <select id="bw-course" className={shared.formInput} {...register("courseId")} autoFocus>
            <option value="">{t("offerings.batch.selectCourse")}</option>
            {courses.map((c) => (
              <option key={c.id} value={c.id}>{c.code} — {c.title}</option>
            ))}
          </select>
          {errors.courseId && <span className={shared.formError}>{errors.courseId.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="bw-prefix">{t("offerings.batch.prefix")}</label>
          <input id="bw-prefix" type="text" className={shared.formInput} placeholder="EVE-" maxLength={28} {...register("sectionPrefix")} />
          <span className={shared.formHint}>{t("offerings.batch.prefixHint")}</span>
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="bw-count">{t("offerings.batch.count")}</label>
          <input id="bw-count" type="number" className={shared.formInput} min={1} max={50} {...register("count")} />
          {errors.count && <span className={shared.formError}>{errors.count.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="bw-capacity">{t("offerings.batch.capacity")}</label>
          <input id="bw-capacity" type="number" className={shared.formInput} min={0} max={9999} {...register("capacity")} />
          {errors.capacity && <span className={shared.formError}>{errors.capacity.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="bw-instructor">{t("offerings.batch.instructor")}</label>
          <select id="bw-instructor" className={shared.formInput} {...register("instructorId")}>
            <option value="">{t("offerings.unassigned")}</option>
            {staff.map((s) => (
              <option key={s.id} value={s.id}>{s.name}{s.jobTitle ? ` — ${s.jobTitle}` : ""}</option>
            ))}
          </select>
        </div>

        {preview && (
          <div className={shared.emptyInline} aria-live="polite">
            {t("offerings.batch.preview", { codes: preview })}
          </div>
        )}

        <button type="submit" style={{ display: "none" }} aria-hidden="true" />
      </form>
    </Drawer>
  );
}
