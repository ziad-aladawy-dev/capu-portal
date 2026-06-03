import { useState, useEffect, useRef } from "react";
import { Bell, ChevronDown, Menu, Building2, CalendarRange, BookOpen, User, Settings, LogOut} from "lucide-react";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../../core/contexts/AuthContext";
import { useScope } from "../../../core/contexts/ScopeContext";
import { ScopeTreeModal } from "../../../modules/university/components/ScopeTreeModal";
import "./navbar.css";

const getLocalizedText = (text, lang) => {
  if (!text) return "";
  try {
    const parsed = JSON.parse(text);
    return parsed[lang] || parsed.ar || parsed.en || text;
  } catch {
    return text;
  }
};

function Navbar({ onToggleSidebar }) {
  const { selectedScope, updateScope } = useScope();
  const [showScopeModal, setShowScopeModal] = useState(false);
  const [showAvatarMenu, setShowAvatarMenu] = useState(false);
  const avatarMenuRef = useRef(null);
  const { t, i18n } = useTranslation();
  const { user, logout } = useAuth();
  const currentLanguage = i18n.language;

  useEffect(() => {
    const handleClickOutside = (event) => {
      if (avatarMenuRef.current && !avatarMenuRef.current.contains(event.target)) {
        setShowAvatarMenu(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    if (i18n.language === 'ar') {
      document.body.classList.add('rtl');
      document.body.dir = 'rtl';
    } else {
      document.body.classList.remove('rtl');
      document.body.dir = 'ltr';
    }
  }, [i18n.language]);

  const changeLanguage = (lng) => {
    i18n.changeLanguage(lng);
    localStorage.setItem('i18nextLng', lng);
  };

  let currentScopeName = t("select_scope");
  if (selectedScope) {
    if (selectedScope.localizedName) {
      currentScopeName = selectedScope.localizedName;
    } else if (selectedScope.name) {
      currentScopeName = getLocalizedText(selectedScope.name, i18n.language);
    } else {
      currentScopeName = t("select_scope");
    }
  }
  const selectedYear = "2025-2026";
  const selectedSemester = "Fall Semester";

  const handleScopeSelect = (node) => {
    updateScope({
      id: node.id,
      name: node.name,
      originalName: node.originalName,
      localizedName: node.localizedName,
      type: node.type,
      path: node.path,
    });
  };

  const getUserInitial = () => {
    if (!user) return "U";
    let displayName = "";
    try {
      if (typeof user.name === 'object' && user.name !== null) {
        displayName = currentLanguage === 'ar' ? (user.name.ar || user.name.en) : (user.name.en || user.name.ar);
      } else {
        const parsed = JSON.parse(user.name);
        displayName = currentLanguage === 'ar' ? (parsed.ar || parsed.en) : (parsed.en || parsed.ar);
      }
    } catch {
      displayName = user.name || "";
    }
    return displayName?.charAt(0)?.toUpperCase() || "U";
  };

  const handleLogout = async () => {
    await logout();
    window.location.href = "/admin/login";
  };

  const handleProfile = () => {
    window.location.href = "/admin/profile";
  };

  const handleSettings = () => {
    window.location.href = "/admin/settings";
  };

  return (
    <header className="navbar">
      <div className="navbar-left">
        <button className="nav-icon-btn" onClick={onToggleSidebar}>
          <Menu size={16} />
        </button>
        <div className="nav-divider" />
        <div className="nav-dropdown-trigger scope-trigger" onClick={() => setShowScopeModal(true)}>
          <Building2 size={15} />
          <div className="scope-content">
            <span className="nav-label">{t("current_scope") || "Current Scope"}</span>
            <strong>{currentScopeName}</strong>
          </div>
          <ChevronDown size={13} />
        </div>
        <div className="nav-dropdown-trigger small">
          <CalendarRange size={15} />
          <div>
            <span className="nav-label">{t("academic_year") || "Academic Year"}</span>
            <strong>{selectedYear}</strong>
          </div>
          <ChevronDown size={13} />
        </div>
        <div className="nav-dropdown-trigger small">
          <BookOpen size={15} />
          <div>
            <span className="nav-label">{t("semester") || "Semester"}</span>
            <strong>{selectedSemester}</strong>
          </div>
          <ChevronDown size={13} />
        </div>
      </div>
      <div className="navbar-right">
        <div className="language-selector">
          <select
            value={currentLanguage}
            onChange={(e) => changeLanguage(e.target.value)}
            className="lang-select"
          >
            <option value="ar">العربية</option>
            <option value="en">English</option>
          </select>
        </div>

        <button className="nav-icon-btn">
          <Bell size={14} />
          <span className="badge" />
        </button>
        
        <div className="avatar-dropdown" ref={avatarMenuRef}>
          <div 
            className="topbar-avatar" 
            onClick={() => setShowAvatarMenu(!showAvatarMenu)}
          >
            {getUserInitial()}
          </div>
          {showAvatarMenu && (
            <div className="avatar-menu">
              <button onClick={handleProfile} className="avatar-menu-item">
                <User size={14} />
                <span>{t("profile")}</span>
              </button>
              <button onClick={handleSettings} className="avatar-menu-item">
                <Settings size={14} />
                <span>{t("settings")}</span>
              </button>
              <hr className="avatar-menu-divider" />
              <button onClick={handleLogout} className="avatar-menu-item logout">
                <LogOut size={14} />
                <span>{t("logout")}</span>
              </button>
            </div>
          )}
        </div>
      </div>

      <ScopeTreeModal
        isOpen={showScopeModal}
        onClose={() => setShowScopeModal(false)}
        onSelect={handleScopeSelect}
        initialScopeId={selectedScope?.id}
      />
    </header>
  );
}

export default Navbar;