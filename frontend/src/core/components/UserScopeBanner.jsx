import { User, X } from "lucide-react";
import { useUserScope } from "../hooks/useUserScope";
import "./userScopeBanner.css";

function UserScopeBanner() {
  const { scopedUser, isScoped, clearScope, isManagementPage } = useUserScope();

  if (!isScoped || !scopedUser || !isManagementPage) return null;

  return (
    <div className="user-scope-banner">
      <div className="user-scope-banner-info">
        <User size={14} />
        <span>
          Showing data for: <strong>{scopedUser.name}</strong>
        </span>
        {scopedUser.code && (
          <span className="user-scope-banner-code">({scopedUser.code})</span>
        )}
        {scopedUser.type && (
          <span className={`user-scope-banner-type type-${scopedUser.type}`}>
            {scopedUser.type === "staff" ? "Staff" : scopedUser.type === "student" ? "Student" : ""}
          </span>
        )}
      </div>
      <button className="user-scope-banner-clear" onClick={clearScope}>
        <X size={12} /> Clear
      </button>
    </div>
  );
}

export default UserScopeBanner;
