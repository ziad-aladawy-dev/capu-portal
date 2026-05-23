import { useState, useEffect } from "react";
import { BookOpen, ExternalLink, AlertCircle } from "lucide-react";
import { useNavigate } from "react-router-dom";

function UserCoursesTab({ userId, userType }) {
  const navigate = useNavigate();
  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (userType !== "student") {
      setLoading(false);
      return;
    }
    // Future: fetch from courses/grades API
    const timer = setTimeout(() => {
      setCourses([]);
      setLoading(false);
    }, 300);
    return () => clearTimeout(timer);
  }, [userId, userType]);

  if (userType !== "student") {
    return (
      <div style={{ textAlign: "center", padding: "40px 20px", color: "#9ca3af" }}>
        <BookOpen size={32} style={{ opacity: 0.3, marginBottom: 8 }} />
        <p style={{ fontSize: 13 }}>Course information is only available for students.</p>
      </div>
    );
  }

  if (loading) {
    return <div style={{ padding: 40, textAlign: "center", color: "#9ca3af" }}>Loading courses…</div>;
  }

  if (courses.length === 0) {
    return (
      <div style={{ textAlign: "center", padding: "40px 20px" }}>
        <BookOpen size={32} style={{ opacity: 0.3, marginBottom: 8, color: "#9ca3af" }} />
        <p style={{ fontSize: 13, color: "#9ca3af" }}>No course data available yet.</p>
        <p style={{ fontSize: 11, color: "#d1d5db", marginTop: 4 }}>
          Course enrollment and grades will appear here once integrated.
        </p>
        <button
          onClick={() => navigate("/admin/courses")}
          style={{
            marginTop: 12, display: "inline-flex", alignItems: "center", gap: 6,
            padding: "7px 12px", borderRadius: 8, border: "none",
            background: "#f0f1f8", color: "#1a1f5e",
            fontSize: 11, fontWeight: 700, cursor: "pointer",
          }}
        >
          <ExternalLink size={13} /> Course Catalog
        </button>
      </div>
    );
  }

  return null;
}

export default UserCoursesTab;
