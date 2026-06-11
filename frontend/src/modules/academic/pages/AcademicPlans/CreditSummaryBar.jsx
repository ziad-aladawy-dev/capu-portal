import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Layers, BookOpen, GraduationCap } from "lucide-react";

/**
 * Shared credit/course summary shown under both the Grid and Table views so the
 * two stay visually coherent. Breaks totals down per level.
 */
export default function CreditSummaryBar({ planCourses, courseMap }) {
  const { t } = useTranslation();

  const summary = useMemo(() => {
    const pcs = planCourses || [];
    const byLevel = {};
    let totalCredits = 0;
    let mandatory = 0;
    for (const pc of pcs) {
      const credits = courseMap[pc.courseId]?.creditHours || 0;
      if (!byLevel[pc.level]) byLevel[pc.level] = { count: 0, credits: 0 };
      byLevel[pc.level].count += 1;
      byLevel[pc.level].credits += credits;
      totalCredits += credits;
      if (pc.isMandatory) mandatory += 1;
    }
    return { byLevel, totalCredits, count: pcs.length, mandatory, elective: pcs.length - mandatory };
  }, [planCourses, courseMap]);

  if (!planCourses || planCourses.length === 0) return null;

  return (
    <div className="aplans-summary-bar">
      <span className="aplans-summary-stat aplans-summary-primary">
        <BookOpen size={13} /> <strong>{summary.count}</strong> {t("courses")}
      </span>
      <span className="aplans-summary-stat aplans-summary-primary">
        <GraduationCap size={13} /> <strong>{summary.totalCredits}</strong> {t("total_credits")}
      </span>
      <span className="aplans-summary-stat aplans-summary-mandatory">
        <strong>{summary.mandatory}</strong> {t("mandatory")}
      </span>
      <span className="aplans-summary-stat aplans-summary-elective">
        <strong>{summary.elective}</strong> {t("elective")}
      </span>
      <span className="aplans-summary-divider" />
      {Object.entries(summary.byLevel)
        .sort(([a], [b]) => Number(a) - Number(b))
        .map(([level, info]) => (
          <span key={level} className="aplans-summary-level">
            <Layers size={11} /> {t("level")} {level}: <strong>{info.credits}</strong> {t("cr")} ({info.count})
          </span>
        ))}
    </div>
  );
}
