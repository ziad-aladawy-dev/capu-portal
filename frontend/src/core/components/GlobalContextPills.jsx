import { Calendar, Building2, GraduationCap } from "lucide-react";
import useAcademicStore from "../stores/useAcademicStore";
import useScopeStore from "../stores/useScopeStore";

export default function GlobalContextPills() {
  const { selectedYear, selectedSemester } = useAcademicStore();
  const { scopeNode } = useScopeStore();

  const pills = [];

  if (scopeNode) {
    pills.push({
      icon: Building2,
      label: scopeNode.name,
      title: `Scope: ${scopeNode.type || "Node"} — ${scopeNode.name}`,
    });
  }

  if (selectedYear && selectedYear !== "—") {
    pills.push({
      icon: Calendar,
      label: selectedYear,
      title: `Academic Year: ${selectedYear}`,
    });
  }

  if (selectedSemester && selectedSemester !== "—") {
    pills.push({
      icon: GraduationCap,
      label: selectedSemester,
      title: `Semester: ${selectedSemester}`,
    });
  }

  if (pills.length === 0) return null;

  return (
    <div
      aria-label="Active context filters"
      style={{
        display: "flex",
        alignItems: "center",
        gap: 6,
        flexWrap: "wrap",
      }}
    >
      {pills.map((pill, idx) => (
        <span
          key={idx}
          role="status"
          title={pill.title}
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: 4,
            padding: "3px 10px",
            background: "#eef2ff",
            border: "1px solid #c7d2fe",
            borderRadius: 20,
            fontSize: 11,
            fontWeight: 600,
            color: "#4338ca",
            whiteSpace: "nowrap",
          }}
        >
          <pill.icon size={12} aria-hidden="true" />
          {pill.label}
        </span>
      ))}
    </div>
  );
}
