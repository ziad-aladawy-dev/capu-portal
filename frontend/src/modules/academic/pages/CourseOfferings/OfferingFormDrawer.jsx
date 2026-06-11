import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslation } from "react-i18next";
import Drawer from "../../../../core/components/Drawer";
import { useActiveCourses } from "../../../../core/query/useCourses";
import {
  OFFERING_STATUS_LABELS, REGISTRATION_STATE_LABELS,
} from "../../../../core/services/courseOfferingService";
import { useStaffOptions } from "./useStaffOptions";
import shared from "../../styles/academic.module.css";

const CLEAR_INSTRUCTOR = "00000000-0000-0000-0000-000000000000"; // PATCH sentinel: clear assignment

/**
 * Create/Edit offering drawer — react-hook-form + zod. Edit sends a sparse
 * PATCH (only changed fields); the instructor uses the Guid.Empty sentinel to
 * clear an assignment.
 */
export default function OfferingFormDrawer({ open, mode, offering, semester, scopeNode, onClose, onSubmit, saving, serverError }) {
  const { t } = useTranslation("academic");
  const { data: courses = [] } = useActiveCourses();
  const { data: staff = [] } = useStaffOptions();

  const schema = z.object({
    courseId: z.string().min(1, t("schedule.validation.offeringRequired")),
    sectionCode: z.string().trim().min(1).max(32),
    capacity: z.coerce.number().int().min(0).max(9999),
    status: z.coerce.number().int().min(0).max(3),
    registrationState: z.coerce.number().int().min(0).max(2),
    instructorId: z.string().optional().or(z.literal("")),
  });

  const { register, handleSubmit, formState: { errors }, reset } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { courseId: "", sectionCode: "", capacity: 30, status: 0, registrationState: 0, instructorId: "" },
  });

  useEffect(() => {
    if (!open) return;
    if (mode === "edit" && offering) {
      reset({
        courseId: offering.courseId,
        sectionCode: offering.sectionCode,
        capacity: offering.capacity,
        status: offering.status,
        registrationState: offering.registrationState,
        instructorId: offering.instructorId || "",
      });
    } else {
      reset({ courseId: "", sectionCode: "", capacity: 30, status: 0, registrationState: 0, instructorId: "" });
    }
  }, [open, mode, offering, reset]);

  const submit = handleSubmit((values) => {
    if (mode === "create") {
      onSubmit({
        courseId: values.courseId,
        semesterId: semester?.id,
        structureNodeId: scopeNode?.id,
        sectionCode: values.sectionCode,
        capacity: values.capacity,
        status: values.status,
        registrationState: values.registrationState,
        instructorId: values.instructorId || null,
      });
    } else {
      const body = {};
      if (values.sectionCode !== offering.sectionCode) body.sectionCode = values.sectionCode;
      if (values.capacity !== offering.capacity) body.capacity = values.capacity;
      if (values.status !== offering.status) body.status = values.status;
      if (values.registrationState !== offering.registrationState) body.registrationState = values.registrationState;
      const current = offering.instructorId || "";
      if ((values.instructorId || "") !== current) {
        body.instructorId = values.instructorId || CLEAR_INSTRUCTOR;
      }
      onSubmit(body);
    }
  });

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={mode === "create" ? t("offerings.createOffering") : t("offerings.editOffering")}
      width={460}
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
      {serverError && <div className={shared.errorBanner} role="alert">{serverError}</div>}

      <form onSubmit={submit}>
        <div className={shared.formGroup}>
          <label htmlFor="of-course">{t("offerings.course")} *</label>
          <select id="of-course" className={shared.formInput} disabled={mode === "edit"} {...register("courseId")}>
            <option value="">{t("offerings.batch.selectCourse")}</option>
            {courses.map((c) => (
              <option key={c.id} value={c.id}>{c.code} — {c.title}</option>
            ))}
          </select>
          {errors.courseId && <span className={shared.formError}>{errors.courseId.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="of-section">{t("offerings.sectionCode")} *</label>
          <input id="of-section" type="text" className={shared.formInput} placeholder="e.g. A" maxLength={32} {...register("sectionCode")} />
          {errors.sectionCode && <span className={shared.formError}>{errors.sectionCode.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="of-capacity">{t("offerings.capacity")}</label>
          <input id="of-capacity" type="number" className={shared.formInput} min={0} max={9999} {...register("capacity")} />
          {errors.capacity && <span className={shared.formError}>{errors.capacity.message}</span>}
        </div>

        <div className={shared.formGroup}>
          <label htmlFor="of-instructor">{t("offerings.instructor")}</label>
          <select id="of-instructor" className={shared.formInput} {...register("instructorId")}>
            <option value="">{t("offerings.unassigned")}</option>
            {staff.map((s) => (
              <option key={s.id} value={s.id}>{s.name}{s.jobTitle ? ` — ${s.jobTitle}` : ""}</option>
            ))}
          </select>
        </div>

        {mode === "edit" && (
          <>
            <div className={shared.formGroup}>
              <label htmlFor="of-status">{t("common.status")}</label>
              <select id="of-status" className={shared.formInput} {...register("status")}>
                {Object.entries(OFFERING_STATUS_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
            <div className={shared.formGroup}>
              <label htmlFor="of-reg">{t("offerings.registration")}</label>
              <select id="of-reg" className={shared.formInput} {...register("registrationState")}>
                {Object.entries(REGISTRATION_STATE_LABELS).map(([val, label]) => (
                  <option key={val} value={val}>{label}</option>
                ))}
              </select>
            </div>
          </>
        )}

        <button type="submit" style={{ display: "none" }} aria-hidden="true" />
      </form>
    </Drawer>
  );
}
