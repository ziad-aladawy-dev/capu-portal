import { useState } from "react";
import { Users, GraduationCap } from "lucide-react";
import { useTranslation } from "react-i18next";
import DirectoryPage from "../../../core/components/directory/DirectoryPage";
import { studentDirectoryConfig, staffDirectoryConfig } from "../../../core/components/directory/directoryConfigs";

function UserManagement() {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState("students");

  return (
    <>
      <div className="users-tabs" style={{ paddingLeft: 16, paddingRight: 16 }}>
        <button
          className={`users-tab ${activeTab === "students" ? "active" : ""}`}
          onClick={() => setActiveTab("students")}
        >
          <GraduationCap size={16} /> {t("students")}
        </button>
        <button
          className={`users-tab ${activeTab === "staff" ? "active" : ""}`}
          onClick={() => setActiveTab("staff")}
        >
          <Users size={16} /> {t("staff")}
        </button>
      </div>
      {activeTab === "students" ? (
        <DirectoryPage config={studentDirectoryConfig} />
      ) : (
        <DirectoryPage config={staffDirectoryConfig} />
      )}
    </>
  );
}

export default UserManagement;
