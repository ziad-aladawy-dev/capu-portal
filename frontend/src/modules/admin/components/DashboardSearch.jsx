import { Search } from "lucide-react";

function DashboardSearch() {
  return (
    <div className="dashboard-search-box">
      <div className="search-wrapper">
        <Search size={16} className="search-icon" />

        <input
          type="text"
          placeholder="Global search..."
          className="search-input"
        />
      </div>
    </div>
  );
}

export default DashboardSearch;