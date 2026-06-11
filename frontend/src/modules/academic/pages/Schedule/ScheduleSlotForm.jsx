import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useTranslation } from "react-i18next";
import shared from "../../styles/academic.module.css";

const fmtTime = (v) => (v ? String(v).slice(0, 5) : "");

/**
 * The single slot form shared by the Schedule builder drawer and the
 * Scheduling Matrix edit drawer (replaces the 3 divergent implementations).
 * react-hook-form + zod; the parent owns the Drawer and triggers submission
 * through `submitRef` so the footer button can live in the Drawer chrome.
 *
 * onSubmit receives { dayOfWeek:number, startTime:"HH:MM", endTime:"HH:MM",
 * kind:number, location:string, notes:string }.
 */
export default function ScheduleSlotForm({ slot, onSubmit, submitRef, extraError }) {
  const { t } = useTranslation("academic");

  const schema = z.object({
    dayOfWeek: z.coerce.number().int().min(0).max(6),
    startTime: z.string().regex(/^\d{2}:\d{2}$/, t("schedule.validation.timeFormat")),
    endTime: z.string().regex(/^\d{2}:\d{2}$/, t("schedule.validation.timeFormat")),
    kind: z.coerce.number().int().min(0).max(5),
    location: z.string().max(200).optional().or(z.literal("")),
    notes: z.string().max(500).optional().or(z.literal("")),
  }).refine((d) => d.endTime > d.startTime, {
    message: t("schedule.validation.endAfterStart"),
    path: ["endTime"],
  });

  const { register, handleSubmit, formState: { errors }, reset } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { dayOfWeek: 0, startTime: "08:00", endTime: "09:00", kind: 0, location: "", notes: "" },
  });

  useEffect(() => {
    if (slot) {
      reset({
        dayOfWeek: slot.dayOfWeek ?? 0,
        startTime: fmtTime(slot.startTime) || "08:00",
        endTime: fmtTime(slot.endTime) || "09:00",
        kind: slot.kind ?? 0,
        location: slot.location || "",
        notes: slot.notes || "",
      });
    } else {
      reset({ dayOfWeek: 0, startTime: "08:00", endTime: "09:00", kind: 0, location: "", notes: "" });
    }
  }, [slot, reset]);

  const submit = handleSubmit((values) =>
    onSubmit({
      dayOfWeek: values.dayOfWeek,
      startTime: values.startTime,
      endTime: values.endTime,
      kind: values.kind,
      location: values.location || "",
      notes: values.notes || "",
    })
  );

  // Let the parent Drawer's footer button trigger submission.
  useEffect(() => {
    if (submitRef) submitRef.current = submit;
  });

  return (
    <form onSubmit={submit}>
      {extraError && <div className={shared.errorBanner} role="alert">{extraError}</div>}

      <div className={shared.formGroup}>
        <label htmlFor="slot-day">{t("schedule.day")}</label>
        <select id="slot-day" className={shared.formInput} {...register("dayOfWeek")}>
          {[0, 1, 2, 3, 4, 5, 6].map((d) => (
            <option key={d} value={d}>{t(`schedule.days.${d}`)}</option>
          ))}
        </select>
      </div>

      <div style={{ display: "flex", gap: 12 }}>
        <div className={shared.formGroup} style={{ flex: 1 }}>
          <label htmlFor="slot-start">{t("schedule.startTime")}</label>
          <input id="slot-start" type="time" className={shared.formInput} {...register("startTime")} />
          {errors.startTime && <span className={shared.formError}>{errors.startTime.message}</span>}
        </div>
        <div className={shared.formGroup} style={{ flex: 1 }}>
          <label htmlFor="slot-end">{t("schedule.endTime")}</label>
          <input id="slot-end" type="time" className={shared.formInput} {...register("endTime")} />
          {errors.endTime && <span className={shared.formError}>{errors.endTime.message}</span>}
        </div>
      </div>

      <div className={shared.formGroup}>
        <label htmlFor="slot-kind">{t("schedule.kind")}</label>
        <select id="slot-kind" className={shared.formInput} {...register("kind")}>
          {[0, 1, 2, 3, 4, 5].map((k) => (
            <option key={k} value={k}>{t(`schedule.kinds.${k}`)}</option>
          ))}
        </select>
      </div>

      <div className={shared.formGroup}>
        <label htmlFor="slot-location">{t("schedule.location")}</label>
        <input id="slot-location" type="text" className={shared.formInput} placeholder="e.g. Bldg A, Room 201" {...register("location")} />
      </div>

      <div className={shared.formGroup}>
        <label htmlFor="slot-notes">{t("schedule.notes")}</label>
        <textarea id="slot-notes" rows={2} className={shared.formInput} style={{ resize: "vertical", fontFamily: "inherit" }} {...register("notes")} />
      </div>

      <button type="submit" style={{ display: "none" }} aria-hidden="true" />
    </form>
  );
}
