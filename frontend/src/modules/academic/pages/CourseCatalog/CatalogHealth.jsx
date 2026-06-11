import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Activity, ChevronDown, ChevronRight } from "lucide-react";
import { useActiveCourses, useAllPrerequisitePairs } from "../../../../core/query/useCourses";
import { useOfferingsForSchedule } from "../../../../core/query/useScheduleSlots";
import shared from "../../styles/academic.module.css";
import styles from "./CourseCatalog.module.css";

/**
 * Cross-references the full active catalog with this semester's offerings and
 * the prerequisite graph. Collapsible — the queries are cheap (active list +
 * edge list are both cached) and only the offerings call varies per semester.
 */
export default function CatalogHealth({ semester, scopeNodeId, onPickCourse }) {
  const { t } = useTranslation("academic");
  const [open, setOpen] = useState(false);

  const { data: allCourses = [] } = useActiveCourses();
  const { data: pairs = [] } = useAllPrerequisitePairs();
  const { data: offerings = [] } = useOfferingsForSchedule(scopeNodeId, semester?.id);

  const report = useMemo(() => {
    const offeredCourseIds = new Set(offerings.map((o) => o.courseId));
    const withPrereqs = new Set(pairs.map((p) => p.courseId));

    const neverOffered = semester
      ? allCourses.filter((c) => c.isActive && !offeredCourseIds.has(c.id))
      : [];
    const noPrereqs = allCourses.filter((c) => !withPrereqs.has(c.id));
    const inactiveWithOfferings = semester
      ? allCourses.filter((c) => !c.isActive && offeredCourseIds.has(c.id))
      : [];

    return { neverOffered, noPrereqs, inactiveWithOfferings };
  }, [allCourses, pairs, offerings, semester]);

  const sections = [
    { key: "neverOffered", items: report.neverOffered, tone: "warn" },
    { key: "inactiveWithOfferings", items: report.inactiveWithOfferings, tone: "warn" },
    { key: "noPrereqs", items: report.noPrereqs, tone: "info" },
  ];

  const issueCount = report.neverOffered.length + report.inactiveWithOfferings.length;

  return (
    <div className={styles.healthPanel}>
      <button type="button" className={styles.healthToggle} onClick={() => setOpen((o) => !o)} aria-expanded={open}>
        {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        <Activity size={14} />
        <span>{t("health.title")}</span>
        {issueCount > 0 && <span className={styles.healthCount}>{issueCount}</span>}
      </button>

      {open && (
        <div className={styles.healthBody}>
          {sections.every((s) => s.items.length === 0) && (
            <div className={shared.emptyInline}>{t("health.empty")}</div>
          )}
          {sections.map((s) =>
            s.items.length === 0 ? null : (
              <div key={s.key} className={styles.healthSection}>
                <div className={styles.healthSectionTitle}>
                  {t(`health.${s.key}`)} <span className={styles.healthSectionCount}>{s.items.length}</span>
                  <span className={styles.healthSectionHint}>{t(`health.${s.key}Hint`)}</span>
                </div>
                <div className={styles.healthChips}>
                  {s.items.slice(0, 30).map((c) => (
                    <button
                      key={c.id}
                      type="button"
                      className={shared.codePill}
                      style={{ cursor: "pointer" }}
                      onClick={() => onPickCourse?.(c)}
                      title={c.title}
                    >
                      {c.code}
                    </button>
                  ))}
                  {s.items.length > 30 && <span className={shared.metaChip}>+{s.items.length - 30}</span>}
                </div>
              </div>
            )
          )}
        </div>
      )}
    </div>
  );
}
