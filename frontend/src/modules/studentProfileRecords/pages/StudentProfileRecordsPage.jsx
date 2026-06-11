import { useNavigate, useParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { FileText, ArrowLeft } from "lucide-react";
import { useStudent } from "../../../core/query/useStudents";
import ProfileRecordsPanel from "../components/ProfileRecordsPanel";
import "../styles/studentProfileRecords.css";

// Entity-scoped wrapper: /admin/students/:studentId/profile-records
function StudentProfileRecordsPage() {
  const { t } = useTranslation();
  const { studentId } = useParams();
  const navigate = useNavigate();
  const studentQuery = useStudent(studentId);
  const student = studentQuery.data;

  return (
    <div className="spr-page">
      <button className="spr-back" onClick={() => navigate(-1)}>
        <ArrowLeft size={14} /> {t("back")}
      </button>

      <div className="spr-header">
        <div className="spr-header-left">
          <FileText size={22} />
          <div>
            <h1>{t("profile_records")}</h1>
            <p>
              {student
                ? <>{t("for_student")} <strong>{student.name || student.fullNameEn}</strong>{student.studentCode && <> · {student.studentCode}</>}</>
                : t("student_id_short", { id: studentId.slice(0, 8) })}
            </p>
          </div>
        </div>
      </div>

      <ProfileRecordsPanel studentId={studentId} />
    </div>
  );
}

export default StudentProfileRecordsPage;
