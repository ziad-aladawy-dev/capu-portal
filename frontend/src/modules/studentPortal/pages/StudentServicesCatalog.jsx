import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Search, AlertCircle, PackageOpen, ArrowRight } from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { useServicesCatalog } from "../hooks/useServicesCatalog";
import { SERVICE_TYPE, SERVICE_TYPE_LABELS } from "../constants/requestStatus";
import { fmtAmount } from "../../../core/services/treasuryPaymentService";
import "../styles/studentServicesCatalog.css";

const TYPE_FILTERS = [
  { value: "", label: "All Types" },
  { value: SERVICE_TYPE.General, label: SERVICE_TYPE_LABELS[SERVICE_TYPE.General] },
  { value: SERVICE_TYPE.Specialized, label: SERVICE_TYPE_LABELS[SERVICE_TYPE.Specialized] },
  { value: SERVICE_TYPE.Administrative, label: SERVICE_TYPE_LABELS[SERVICE_TYPE.Administrative] },
];

function StudentServicesCatalog() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const navigate = useNavigate();
  const { data, isLoading, isError } = useServicesCatalog(user?.id);

  const [search, setSearch] = useState("");
  const [type, setType] = useState("");
  const [fee, setFee] = useState(""); // "", "free", "paid"
  const [sort, setSort] = useState("name");

  const services = useMemo(() => {
    let list = Array.isArray(data) ? [...data] : [];
    const q = search.trim().toLowerCase();
    if (q) list = list.filter((s) => `${s.name} ${s.description || ""}`.toLowerCase().includes(q));
    if (type !== "") list = list.filter((s) => s.type === Number(type));
    if (fee === "free") list = list.filter((s) => !s.isPaid);
    if (fee === "paid") list = list.filter((s) => s.isPaid);
    list.sort((a, b) =>
      sort === "price"
        ? (a.price || 0) - (b.price || 0)
        : String(a.name).localeCompare(String(b.name))
    );
    return list;
  }, [data, search, type, fee, sort]);

  return (
    <div className="ssc-container">
      <div className="ssc-header">
        <h1>{t("services.catalog_title", { defaultValue: "Student Services" })}</h1>
        <p>{t("services.catalog_subtitle", { defaultValue: "Browse and request available university services" })}</p>
      </div>

      <div className="ssc-toolbar">
        <div className="ssc-search">
          <Search size={16} />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("services.search", { defaultValue: "Search services…" })}
          />
        </div>
        <select value={type} onChange={(e) => setType(e.target.value)}>
          {TYPE_FILTERS.map((f) => (
            <option key={f.value} value={f.value}>{t(`services.type_${f.value || "all"}`, { defaultValue: f.label })}</option>
          ))}
        </select>
        <select value={fee} onChange={(e) => setFee(e.target.value)}>
          <option value="">{t("services.fee_all", { defaultValue: "All Fees" })}</option>
          <option value="free">{t("services.fee_free", { defaultValue: "Free" })}</option>
          <option value="paid">{t("services.fee_paid", { defaultValue: "Paid" })}</option>
        </select>
        <select value={sort} onChange={(e) => setSort(e.target.value)}>
          <option value="name">{t("services.sort_name", { defaultValue: "Sort: Name" })}</option>
          <option value="price">{t("services.sort_price", { defaultValue: "Sort: Price" })}</option>
        </select>
      </div>

      {isLoading ? (
        <div className="ssc-state ssc-muted">{t("common.loading", { defaultValue: "Loading…" })}</div>
      ) : isError ? (
        <div className="ssc-state ssc-error"><AlertCircle size={18} /> {t("services.load_error", { defaultValue: "Couldn't load services." })}</div>
      ) : services.length === 0 ? (
        <div className="ssc-state ssc-empty">
          <PackageOpen size={32} />
          <p>{t("services.none", { defaultValue: "No services match your search." })}</p>
        </div>
      ) : (
        <div className="ssc-grid">
          {services.map((s) => (
            <button key={s.id} className="ssc-card" onClick={() => navigate(`/student/services/${s.id}`)}>
              <div className="ssc-card-top">
                <span className="ssc-type">{SERVICE_TYPE_LABELS[s.type] || "—"}</span>
                <span className={`ssc-fee ${s.isPaid ? "paid" : "free"}`}>
                  {s.isPaid ? `${fmtAmount(s.price)} EGP` : t("services.free", { defaultValue: "Free" })}
                </span>
              </div>
              <h3 className="ssc-name">{s.name}</h3>
              {s.description && <p className="ssc-desc">{s.description}</p>}
              <span className="ssc-apply">
                {t("services.view", { defaultValue: "View & Apply" })} <ArrowRight size={14} />
              </span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}

export default StudentServicesCatalog;
