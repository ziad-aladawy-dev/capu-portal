import { useTranslation } from "react-i18next";
import { TrendingUp } from "lucide-react";
import PortalCard from "../shared/PortalCard";
import PortalSectionHeader from "../shared/PortalSectionHeader";
import { GRADE_POINTS } from "../../../../core/services/gradeService";
import styles from "./GpaTrendChart.module.css";

/**
 * Per-semester GPA bars computed client-side from the grade history
 * (credit-weighted mean of graded courses). Pure CSS — no chart dependency.
 */
function semesterGpa(courses) {
  let points = 0;
  let credits = 0;
  for (const c of courses || []) {
    const p = GRADE_POINTS[c.grade];
    if (p == null || !c.creditHours) continue;
    points += p * c.creditHours;
    credits += c.creditHours;
  }
  return credits > 0 ? points / credits : null;
}

function GpaTrendChart({ history }) {
  const { t } = useTranslation();

  const bars = (history || [])
    .map((s) => ({ name: s.semesterName, gpa: semesterGpa(s.courses) }))
    .filter((b) => b.gpa != null)
    .reverse(); // oldest → newest

  if (bars.length < 2) return null; // a single point is not a trend

  return (
    <PortalCard className={styles.card}>
      <PortalSectionHeader
        icon={TrendingUp}
        title={t("portal_grades.gpa_trend", { defaultValue: "GPA Trend" })}
      />
      <div className={styles.chart} role="img" aria-label={t("portal_grades.gpa_trend", { defaultValue: "GPA Trend" })}>
        {bars.map((b, i) => {
          const pct = Math.max(4, (b.gpa / 4) * 100);
          const tone = b.gpa >= 3 ? styles.good : b.gpa >= 2 ? styles.mid : styles.low;
          return (
            <div key={i} className={styles.col}>
              <span className={styles.value}>{b.gpa.toFixed(2)}</span>
              <div className={styles.track}>
                <div className={`${styles.bar} ${tone}`} style={{ height: `${pct}%` }} />
              </div>
              <span className={styles.label}>{b.name}</span>
            </div>
          );
        })}
      </div>
    </PortalCard>
  );
}

export default GpaTrendChart;
