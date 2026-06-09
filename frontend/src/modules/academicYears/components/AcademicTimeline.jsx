import { CheckCircle, Lock, Unlock, Trash2, Clock } from "lucide-react";
import PermissionGate from "../../../core/auth/PermissionGate";

function AcademicTimeline({ years, onSetCurrent, onCloseYear, onReopenYear, onDeleteYear, onManageSemesters, lifecycleLoading }) {
  if (!years || years.length === 0) {
    return <div className="empty-state"><p>No academic years to display.</p></div>;
  }

  const allDates = years.flatMap((y) => [new Date(y.startDate).getTime(), new Date(y.endDate).getTime()]);
  const minTime = Math.min(...allDates);
  const maxTime = Math.max(...allDates);
  const totalSpan = maxTime - minTime || 1;

  const getBarWidth = (start, end) => {
    const s = new Date(start).getTime();
    const e = new Date(end).getTime();
    const left = ((s - minTime) / totalSpan) * 100;
    const width = ((e - s) / totalSpan) * 100;
    return { left: Math.max(0, left), width: Math.max(2, Math.min(100 - left, width)) };
  };

  const sorted = [...years].sort((a, b) => new Date(b.startDate) - new Date(a.startDate));

  return (
    <div className="ay-timeline">
      <div className="ay-timeline-header">
        <h3>Year Timeline</h3>
        <span className="ay-timeline-count">{years.length} year(s)</span>
      </div>
      <div className="ay-timeline-years">
        {sorted.map((year) => {
          const { left, width } = getBarWidth(year.startDate, year.endDate);
          const isCurrent = year.isCurrent;
          const isClosed = year.status === "Closed" || year.status === "closed";

          return (
            <div key={year.id} className={`ay-timeline-year ${isCurrent ? "is-current" : ""}`}>
              <div className="ay-timeline-year-label">
                <span className="year-name">{year.name}</span>
                <span className="year-dates">
                  {new Date(year.startDate).toLocaleDateString()} – {new Date(year.endDate).toLocaleDateString()}
                </span>
              </div>

              <div className="ay-timeline-year-bar-wrap">
                <div className="ay-timeline-bar">
                  <div
                    className={`ay-timeline-bar-fill ${isClosed ? "closed" : "current"}`}
                    style={{ left: `${left}%`, width: `${width}%`, position: "absolute" }}
                  >
                    <span className="bar-label">{year.semesters?.length || 0} sem</span>
                  </div>
                </div>
              </div>

              <div className="ay-timeline-year-actions">
                {!isCurrent && !isClosed && (
                  <PermissionGate resource="academics.academic-years" minLevel={3}>
                    <button
                      className="ay-timeline-action-btn set-current"
                      onClick={() => onSetCurrent(year)}
                      disabled={lifecycleLoading === year.id}
                      title="Set as Current Year"
                    >
                      <CheckCircle size={14} />
                    </button>
                  </PermissionGate>
                )}
                <PermissionGate resource="academics.academic-years" minLevel={3}>
                  <button
                    className="ay-timeline-action-btn"
                    onClick={() => onManageSemesters?.(year)}
                    title="Manage Semesters"
                  >
                    <Clock size={14} />
                  </button>
                </PermissionGate>
                {isCurrent ? (
                  <PermissionGate resource="academics.academic-years" minLevel={3}>
                    <button
                      className="ay-timeline-action-btn close"
                      onClick={() => onCloseYear(year)}
                      disabled={lifecycleLoading === year.id}
                      title="Close Year"
                    >
                      <Lock size={14} />
                    </button>
                  </PermissionGate>
                ) : isClosed ? (
                  <PermissionGate resource="academics.academic-years" minLevel={4}>
                    <button
                      className="ay-timeline-action-btn reopen"
                      onClick={() => onReopenYear(year)}
                      disabled={lifecycleLoading === year.id}
                      title="Reopen Year"
                    >
                      <Unlock size={14} />
                    </button>
                  </PermissionGate>
                ) : null}
                <PermissionGate resource="academics.academic-years" minLevel={5}>
                  <button
                    className="ay-timeline-action-btn delete"
                    onClick={() => onDeleteYear?.(year)}
                    title="Delete"
                  >
                    <Trash2 size={14} />
                  </button>
                </PermissionGate>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

export default AcademicTimeline;
