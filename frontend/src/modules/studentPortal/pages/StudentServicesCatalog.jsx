import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Search, AlertCircle, PackageOpen, ArrowRight } from "lucide-react";
import { useAuth } from "../../../core/auth/useAuth";
import { useServicesCatalog } from "../hooks/useServicesCatalog";
import { SERVICE_TYPE, SERVICE_TYPE_LABELS } from "../constants/requestStatus";
import { fmtAmount } from "../../../core/services/treasuryPaymentService";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import styles from "./StudentServicesCatalog.module.css";

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
  const { data, isLoading, isError, refetch } = useServicesCatalog(user?.id);

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
    <PortalPageShell
      title={t("portal_services.title", { defaultValue: "Student Services" })}
      subtitle={t("portal_services.subtitle", { defaultValue: "Browse and request available university services" })}
    >
      <div className={styles.toolbar}>
        <div className={styles.search}>
          <Search size={16} />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("portal_services.search", { defaultValue: "Search services…" })}
          />
        </div>
        <select className={styles.select} value={type} onChange={(e) => setType(e.target.value)}>
          {TYPE_FILTERS.map((f) => (
            <option key={f.value} value={f.value}>
              {t(`portal_services.type_${f.value === "" ? "all" : f.value}`, { defaultValue: f.label })}
            </option>
          ))}
        </select>
        <select className={styles.select} value={fee} onChange={(e) => setFee(e.target.value)}>
          <option value="">{t("portal_services.fee_all", { defaultValue: "All Fees" })}</option>
          <option value="free">{t("portal_services.fee_free", { defaultValue: "Free" })}</option>
          <option value="paid">{t("portal_services.fee_paid", { defaultValue: "Paid" })}</option>
        </select>
        <select className={styles.select} value={sort} onChange={(e) => setSort(e.target.value)}>
          <option value="name">{t("portal_services.sort_name", { defaultValue: "Sort: Name" })}</option>
          <option value="price">{t("portal_services.sort_price", { defaultValue: "Sort: Price" })}</option>
        </select>
      </div>

      {isLoading ? (
        <div className={styles.grid}>
          {Array.from({ length: 6 }).map((_, i) => <PortalSkeleton key={i} variant="block" height={150} />)}
        </div>
      ) : isError ? (
        <PortalEmptyState
          icon={AlertCircle}
          title={t("portal_services.load_error", { defaultValue: "Couldn't load services." })}
          onAction={() => refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      ) : services.length === 0 ? (
        <PortalEmptyState
          icon={PackageOpen}
          title={t("portal_services.none", { defaultValue: "No services match your search." })}
        />
      ) : (
        <div className={styles.grid}>
          {services.map((s) => (
            <button
              type="button"
              key={s.id}
              className={styles.card}
              onClick={() => navigate(`/student/services/${s.id}`)}
            >
              <div className={styles.cardTop}>
                <span className={styles.type}>
                  {SERVICE_TYPE_LABELS[s.type]
                    ? t(`portal_services.type_${s.type}`, { defaultValue: SERVICE_TYPE_LABELS[s.type] })
                    : "—"}
                </span>
                <span className={`${styles.fee} ${s.isPaid ? styles.feePaid : styles.feeFree}`}>
                  {s.isPaid
                    ? `${fmtAmount(s.price)} EGP`
                    : t("portal_services.free", { defaultValue: "Free" })}
                </span>
              </div>
              <h3 className={styles.name}>{s.name}</h3>
              {s.description && <p className={styles.desc}>{s.description}</p>}
              <span className={styles.apply}>
                {t("portal_services.view", { defaultValue: "View & Apply" })} <ArrowRight size={14} />
              </span>
            </button>
          ))}
        </div>
      )}
    </PortalPageShell>
  );
}

export default StudentServicesCatalog;
