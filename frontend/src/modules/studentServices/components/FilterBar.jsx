import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Search, Filter, X } from "lucide-react";
import "../styles/components/FilterBar.css";

const FilterBar = ({ filters = [], onApply, onClear, placeholder }) => {
  const { t } = useTranslation();
  const [showAdvanced, setShowAdvanced] = useState(false);
  const [values, setValues] = useState({});

  const handleChange = (key, value) => {
    setValues((prev) => ({ ...prev, [key]: value }));
  };

  const handleApply = () => {
    onApply(values);
  };

  const handleClear = () => {
    setValues({});
    onClear();
  };

  return (
    <div className="filter-bar">
      <div className="filter-bar-row">
        <div className="filter-search">
          <Search size={16} />
          <input
            type="text"
            placeholder={placeholder || t("search_anything")}
            value={values.search || ""}
            onChange={(e) => handleChange("search", e.target.value)}
            onKeyDown={(e) => e.key === "Enter" && handleApply()}
          />
          {values.search && (
            <button onClick={() => handleChange("search", "")} className="filter-clear-search">
              <X size={14} />
            </button>
          )}
        </div>
        <button className="filter-btn soft" onClick={() => setShowAdvanced(!showAdvanced)}>
          <Filter size={14} /> {t("filters")}
        </button>
        <button className="filter-btn primary" onClick={handleApply}>
          {t("apply")}
        </button>
        <button className="filter-btn ghost" onClick={handleClear}>
          {t("clear")}
        </button>
      </div>
      {showAdvanced && (
        <div className="filter-advanced">
          {filters.map((filter) => (
            <div key={filter.key} className="filter-field">
              <label>{filter.label}</label>
              {filter.type === "select" ? (
                <select
                  value={values[filter.key] || ""}
                  onChange={(e) => handleChange(filter.key, e.target.value)}
                >
                  <option value="">{t("all")}</option>
                  {filter.options?.map((opt) => (
                    <option key={opt.value} value={opt.value}>
                      {opt.label}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type={filter.type || "text"}
                  value={values[filter.key] || ""}
                  onChange={(e) => handleChange(filter.key, e.target.value)}
                  placeholder={filter.placeholder}
                />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default FilterBar;