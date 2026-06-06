import React from "react";
import { useTranslation } from "react-i18next";
import { Inbox } from "lucide-react";
import "../styles/components/EmptyState.css";

const EmptyState = ({ message, icon: Icon = Inbox }) => {
  const { t } = useTranslation();
  return (
    <div className="empty-state-container">
      <Icon size={48} className="empty-icon" />
      <p>{message || t("no_data")}</p>
    </div>
  );
};

export default EmptyState;