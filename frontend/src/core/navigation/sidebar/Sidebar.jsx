import { useState, useMemo } from "react";
import { NavLink } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../../core/contexts/AuthContext";
import {
  ChevronRight,
  LogOut,
  Building2,
  Plug,
  LayoutDashboard,
  Users,
  UserPlus,
  Shield,
  Bell,
  GraduationCap,
  Briefcase,
  Settings,
} from "lucide-react";
import "./sidebar.css";
import universityLogo from "../../../../public/images/UniLogo2.png";

const categoryIcons = {
  overview: <LayoutDashboard size={14} />,
  management: <Settings size={14} />,
  people: <Users size={14} />,
  integration: <Plug size={14} />,
  studentServices: <GraduationCap size={14} />,
};

const getLocalizedText = (text, lang) => {
  if (!text) return "";
  try {
    const parsed = JSON.parse(text);
    return parsed[lang] || parsed.ar || parsed.en || text;
  } catch {
    return text;
  }
};

function Sidebar({ isOpen, isMobile, onClose }) {
  const { t, i18n } = useTranslation();
  const { user, logout } = useAuth();
  const [openedCategory, setOpenedCategory] = useState("overview");
  const isRtl = i18n.language === 'ar';

  const userDisplayName = useMemo(() => {
    if (!user) return t("guest");
    
    if (typeof user.name === 'object' && user.name !== null) {
      return i18n.language === 'ar' 
        ? (user.name.ar || user.name.en || t("guest"))
        : (user.name.en || user.name.ar || t("guest"));
    }
    
    if (typeof user.name === 'string') {
      try {
        const parsed = JSON.parse(user.name);
        return i18n.language === 'ar'
          ? (parsed.ar || parsed.en || t("guest"))
          : (parsed.en || parsed.ar || t("guest"));
      } catch {
        return user.name || t("guest");
      }
    }
    
    return t("guest");
  }, [user, i18n.language, t]);

  const getUserAvatar = () => {
    const firstChar = userDisplayName?.charAt(0)?.toUpperCase() || "U";
    return firstChar;
  };

  const userRoleDisplay = useMemo(() => {
    if (!user) return "";
    const role = user.role || "Staff";
    const roleMap = {
      "Super Admin": isRtl ? "مدير عام" : "Super Admin",
      "Staff": isRtl ? "موظف" : "Staff",
      "Student": isRtl ? "طالب" : "Student",
    };
    return roleMap[role] || role;
  }, [user, isRtl]);

  const handleLogout = async () => {
    if (logout) await logout();
    window.location.href = "/admin/login";
  };
  
  const categories = [
    {
      key: "overview",
      title: t("overview"),
      items: [{ label: t("dashboard"), path: "/admin/dashboard", icon: <LayoutDashboard size={13} /> }],
    },
    {
      key: "management",
      title: t("management"),
      items: [
        { label: t("university_structure"), path: "/admin/university-structure", icon: <Building2 size={13} /> },
        { label: t("users"), path: "/admin/users", icon: <Users size={13} /> },
        { label: t("permissions"), path: "/admin/permissions", icon: <Shield size={13} /> },
      ],
    },
    {
      key: "people",
      title: t("people_management"),
      items: [
        { label: t("students_management"), path: "/admin/users?role=Student", icon: <GraduationCap size={13} /> },
        { label: t("staff_management"), path: "/admin/users?role=Staff", icon: <Briefcase size={13} /> },
        { label: t("add_student"), path: "/admin/users/add-student", icon: <UserPlus size={13} /> },
        { label: t("add_staff"), path: "/admin/users/add-staff", icon: <UserPlus size={13} /> },
      ],
    },
    {
      key: "studentServices",
      title: t("student_services"),
      items: [
        { label: t("dashboard"), path: "/admin/student-services/dashboard", icon: <LayoutDashboard size={13} /> },
        { label: t("services_management"), path: "/admin/student-services/services", icon: <Shield size={13} /> },
        { label: t("requests"), path: "/admin/student-services/requests", icon: <Users size={13} /> },
        { label: t("notifications"), path: "/admin/student-services/notifications", icon: <Bell size={13} /> },
      ],
    },
  ];

  const handleFeatureClick = () => {
    if (isMobile && onClose) onClose();
  };

  return (
    <aside
      className={`sidebar ${isOpen ? "is-open" : "is-closed"} ${isRtl ? "rtl" : ""}`}
      dir={isRtl ? "rtl" : "ltr"}
    >
      <svg className="sidebar-geo" viewBox="0 0 230 620" preserveAspectRatio="none">
        <circle cx="230" cy="0" r="140" fill="rgba(224,192,106,0.04)" />
        <circle cx="0" cy="460" r="110" fill="rgba(35,42,116,0.35)" />
        <line x1="115" y1="80" x2="230" y2="200" stroke="rgba(224,192,106,0.06)" strokeWidth="1" />
        <line x1="0" y1="300" x2="115" y2="180" stroke="rgba(224,192,106,0.04)" strokeWidth="1" />
      </svg>

      <div className="sidebar-brand">
        <div className="sidebar-logo-mark">
          <img src={universityLogo} alt="Capital University" className="sidebar-logo-img" />
        </div>
        <div className="sidebar-university-name">{t("app_name")}</div>
        <div className="sidebar-university-sub">{t("control_panel")}</div>
      </div>

      <div className="sidebar-user-section">
        <div className="sidebar-user-card">
          <div className="sidebar-avatar">{getUserAvatar()}</div>
          <div className="sidebar-user-info">
            <strong>{userDisplayName}</strong>
            <span>{userRoleDisplay}</span>
          </div>
          <div className="sidebar-user-badge" />
        </div>
      </div>

      <div className="sidebar-content">
        <div className="sidebar-section-label">{t("menu")}</div>
        {categories.map((category) => {
          const opened = openedCategory === category.key;
          return (
            <div className="sidebar-category" key={category.key}>
              <button
                type="button"
                className={`sidebar-category-header ${opened ? "is-open" : ""}`}
                onClick={() => setOpenedCategory(opened ? null : category.key)}
              >
                <div className="sidebar-cat-icon">{categoryIcons[category.key]}</div>
                <span className="sidebar-category-title">{category.title}</span>
                <ChevronRight size={11} className="sidebar-cat-arrow" />
              </button>

              {opened && (
                <div className="sidebar-features">
                  {category.items.map((item) => (
                    <NavLink
                      key={item.path}
                      to={item.path}
                      end={item.path === "/admin/users" || item.path === "/admin/dashboard"}
                      onClick={handleFeatureClick}
                      className={({ isActive }) =>
                        `sidebar-feature ${isActive ? "active" : ""}`
                      }
                    >
                      <span className="sidebar-feature-dot" />
                      {item.icon}
                      <span>{item.label}</span>
                    </NavLink>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="sidebar-footer">
        <button className="sidebar-footer-btn" onClick={handleLogout}>
          <LogOut size={14} />
          <span>{t("logout")}</span>
        </button>
      </div>
    </aside>
  );
}

export default Sidebar;
