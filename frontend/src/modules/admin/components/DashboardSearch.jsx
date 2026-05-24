import { useState } from "react";
import { Search } from "lucide-react";

function DashboardSearch() {
  const [query, setQuery] = useState("");

  return (
    <div className="dashboard-search-box">
      <div className="search-wrapper">
        <Search size={16} className="search-icon" />

        <input
          type="text"
          placeholder="Global search..."
          className="search-input"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </div>
    </div>
  );
}

export default DashboardSearch;