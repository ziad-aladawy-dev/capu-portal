import { Search } from "lucide-react";
import { useTranslation } from "react-i18next";

function DashboardSearch() {
  const { t } = useTranslation();

  return (
    <div className="dashboard-search-box">
      <div className="search-wrapper">
        <Search size={16} className="search-icon" />
        <input
          type="text"
          placeholder={t("global_search")}
          className="search-input"
        />
      </div>
    </div>
  );
}

export default DashboardSearch;