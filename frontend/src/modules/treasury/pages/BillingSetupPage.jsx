import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import {
  ReceiptText, Link2, Search, RefreshCw, Plus, PowerOff,
  AlertCircle, CloudDownload, CheckCircle2, X,
} from "lucide-react";
import PageHeader from "../../../core/components/PageHeader";
import DataTable from "../../../core/components/DataTable";
import Drawer from "../../../core/components/Drawer";
import ConfirmDialog from "../../../core/components/ConfirmDialog";
import StatusBadge from "../../../core/components/StatusBadge";
import { useToast } from "../../../core/components/Toast";
import PermissionGate from "../../../core/auth/PermissionGate";
import * as studentServicesService from "../../studentServices/services/studentServicesService";
import {
  useReceipts, useLiveReceipts, useServiceMappings,
  useCreateServiceMapping, useDeactivateServiceMapping,
} from "../../../core/query/useTreasury";
import {
  QUANTITY_MODE, QUANTITY_MODE_KEYS, fmtAmount, apiErrorMessage,
} from "../../../core/services/treasuryService";
import "../styles/treasury.css";

const EMPTY_MAPPING_FORM = {
  studentServiceId: "",
  receiptId: "",
  quantityMode: QUANTITY_MODE.Fixed,
};

export default function BillingSetupPage() {
  const { t } = useTranslation();
  const [tab, setTab] = useState("receipts");

  return (
    <div className="ty-page">
      <PageHeader
        icon={ReceiptText}
        title={t("treasury_billing_setup_title")}
        subtitle={t("treasury_billing_setup_subtitle")}
      />

      <div className="ty-tabs" role="tablist">
        <button
          role="tab"
          aria-selected={tab === "receipts"}
          className={`ty-tab${tab === "receipts" ? " active" : ""}`}
          onClick={() => setTab("receipts")}
        >
          <ReceiptText size={14} aria-hidden="true" /> {t("treasury_receipt_catalog_tab")}
        </button>
        <button
          role="tab"
          aria-selected={tab === "mappings"}
          className={`ty-tab${tab === "mappings" ? " active" : ""}`}
          onClick={() => setTab("mappings")}
        >
          <Link2 size={14} aria-hidden="true" /> {t("treasury_service_mappings_tab")}
        </button>
      </div>

      {tab === "receipts" ? <ReceiptsTab /> : <MappingsTab />}
    </div>
  );
}

/* ────────────────────────────────────────────────────────────────
   Receipts — synced catalog + on-demand live drift check
   ──────────────────────────────────────────────────────────────── */

function ReceiptsTab() {
  const { t } = useTranslation();
  const [search, setSearch] = useState("");
  const [showDrift, setShowDrift] = useState(false);

  const {
    data: receipts = [],
    isLoading,
    error,
    refetch,
    isFetching,
  } = useReceipts();
  const live = useLiveReceipts();

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase();
    if (!term) return receipts;
    return receipts.filter(
      (r) =>
        r.name?.toLowerCase().includes(term) ||
        r.externalReceiptId?.toLowerCase().includes(term)
    );
  }, [receipts, search]);

  const activeCount = receipts.filter((r) => r.isActive).length;
  const currencyCount = new Set(receipts.map((r) => r.currency)).size;

  const drift = useMemo(() => {
    const liveRows = live.data || [];
    if (!liveRows.length) return null;
    const localByExternal = {};
    for (const r of receipts) localByExternal[r.externalReceiptId] = r;
    const changed = [];
    const missingLocally = [];
    for (const lr of liveRows) {
      const local = localByExternal[String(lr.receiptId)];
      if (!local) {
        missingLocally.push(lr);
      } else if (
        Number(local.unitAmount) !== Number(lr.amount) ||
        local.isActive !== lr.isActive ||
        local.name !== lr.name
      ) {
        changed.push({ local, live: lr });
      }
    }
    return { changed, missingLocally, liveCount: liveRows.length };
  }, [live.data, receipts]);

  const checkLive = async () => {
    setShowDrift(true);
    await live.refetch();
  };

  const columns = [
    {
      key: "name",
      label: t("treasury_receipt_name"),
      render: (v) => <span className="ty-cell-strong">{v}</span>,
    },
    {
      key: "externalReceiptId",
      label: t("treasury_external_id"),
      render: (v) => <span className="ty-code-pill">{v}</span>,
    },
    {
      key: "unitAmount",
      label: t("treasury_unit_price"),
      align: "right",
      render: (v, row) => (
        <span className="ty-amount-cell">
          {fmtAmount(v)} <span className="ty-currency">{row.currency}</span>
        </span>
      ),
    },
    {
      key: "isActive",
      label: t("status"),
      render: (v) => (
        <StatusBadge
          status={v ? "active" : "inactive"}
          label={v ? t("active") : t("inactive")}
        />
      ),
    },
    {
      key: "lastSyncedAt",
      label: t("treasury_last_synced"),
      render: (v) => (v ? new Date(v).toLocaleString() : "—"),
    },
  ];

  return (
    <>
      <div className="ty-stats">
        <div className="ty-stat">
          <div className="ty-stat-value">{receipts.length}</div>
          <div className="ty-stat-label">{t("treasury_receipts_total")}</div>
        </div>
        <div className="ty-stat-divider" />
        <div className="ty-stat">
          <div className="ty-stat-value ty-success">{activeCount}</div>
          <div className="ty-stat-label">{t("active")}</div>
        </div>
        <div className="ty-stat-divider" />
        <div className="ty-stat">
          <div className="ty-stat-value">{currencyCount}</div>
          <div className="ty-stat-label">{t("treasury_currencies")}</div>
        </div>
      </div>

      <div className="ty-toolbar">
        <div className="ty-search">
          <Search size={15} aria-hidden="true" />
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("treasury_search_receipts")}
            aria-label={t("treasury_search_receipts")}
          />
        </div>
        <button className="ty-btn" onClick={() => refetch()}>
          <RefreshCw size={14} className={isFetching ? "ty-spin" : ""} aria-hidden="true" />
          {t("refresh")}
        </button>
        <button className="ty-btn" onClick={checkLive} disabled={live.isFetching}>
          <CloudDownload size={14} aria-hidden="true" />
          {live.isFetching ? t("treasury_checking_live") : t("treasury_check_live_feed")}
        </button>
      </div>

      <p className="ty-note">{t("treasury_receipts_sync_note")}</p>

      {showDrift && (
        <div className="ty-drift-panel">
          <div className="ty-drift-head">
            <strong>{t("treasury_live_comparison")}</strong>
            <button
              className="ty-icon-btn"
              onClick={() => setShowDrift(false)}
              aria-label={t("close")}
            >
              <X size={14} />
            </button>
          </div>
          {live.isFetching && <p className="ty-note">{t("treasury_checking_live")}…</p>}
          {live.error && (
            <div className="ty-alert ty-alert-error" role="alert">
              <AlertCircle size={14} aria-hidden="true" />
              <span>{apiErrorMessage(live.error, t("treasury_live_feed_failed"))}</span>
            </div>
          )}
          {drift && !live.isFetching && !live.error && (
            drift.changed.length === 0 && drift.missingLocally.length === 0 ? (
              <div className="ty-alert ty-alert-success">
                <CheckCircle2 size={14} aria-hidden="true" />
                <span>{t("treasury_in_sync", { count: drift.liveCount })}</span>
              </div>
            ) : (
              <>
                {drift.changed.map(({ local, live: lr }) => (
                  <div key={local.id} className="ty-drift-row">
                    <span className="ty-cell-strong">{local.name}</span>
                    <span className="ty-drift-diff">
                      {Number(local.unitAmount) !== Number(lr.amount) && (
                        <>
                          {fmtAmount(local.unitAmount)} → <strong>{fmtAmount(lr.amount)}</strong> {lr.currency}
                        </>
                      )}
                      {local.isActive !== lr.isActive && (
                        <span className="ty-meta-chip">
                          {lr.isActive ? t("treasury_now_active_upstream") : t("treasury_now_inactive_upstream")}
                        </span>
                      )}
                      {local.name !== lr.name && (
                        <span className="ty-meta-chip">{t("treasury_renamed_upstream", { name: lr.name })}</span>
                      )}
                    </span>
                  </div>
                ))}
                {drift.missingLocally.map((lr) => (
                  <div key={lr.receiptId} className="ty-drift-row">
                    <span className="ty-cell-strong">{lr.name}</span>
                    <span className="ty-drift-diff">
                      <span className="ty-meta-chip ty-warn">{t("treasury_not_synced_yet")}</span>
                      {fmtAmount(lr.amount)} {lr.currency}
                    </span>
                  </div>
                ))}
                <p className="ty-note">{t("treasury_drift_note")}</p>
              </>
            )
          )}
        </div>
      )}

      <DataTable
        columns={columns}
        data={filtered}
        loading={isLoading}
        error={error ? apiErrorMessage(error, t("treasury_receipts_load_failed")) : null}
        emptyIcon={ReceiptText}
        emptyTitle={t("treasury_no_receipts_title")}
        emptyMessage={t("treasury_no_receipts_hint")}
        tableLabel={t("treasury_receipt_catalog_tab")}
      />
    </>
  );
}

/* ────────────────────────────────────────────────────────────────
   Service → receipt mappings
   ──────────────────────────────────────────────────────────────── */

function MappingsTab() {
  const { t } = useTranslation();
  const { addToast } = useToast();

  const { data: mappings = [], isLoading, error } = useServiceMappings();
  const { data: receipts = [] } = useReceipts();
  const { data: services = [] } = useQuery({
    queryKey: ["student-services", "list"],
    queryFn: () => studentServicesService.getServices(),
    select: (data) => (Array.isArray(data) ? data : []),
    staleTime: 60 * 1000,
  });

  const createMapping = useCreateServiceMapping();
  const deactivateMapping = useDeactivateServiceMapping();

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [form, setForm] = useState(EMPTY_MAPPING_FORM);
  const [formError, setFormError] = useState("");
  const [deactivateTarget, setDeactivateTarget] = useState(null);

  const serviceById = useMemo(() => {
    const map = {};
    for (const s of services) map[s.id] = s;
    return map;
  }, [services]);

  const mappedServiceIds = useMemo(
    () => new Set(mappings.filter((m) => m.isActive).map((m) => m.studentServiceId)),
    [mappings]
  );

  /* Only active, not-yet-mapped services are offered — the backend enforces
     one active mapping per service, so don't let the form run into the 409. */
  const selectableServices = useMemo(
    () => services.filter((s) => s.isActive && !mappedServiceIds.has(s.id)),
    [services, mappedServiceIds]
  );
  const activeReceipts = useMemo(
    () => receipts.filter((r) => r.isActive),
    [receipts]
  );

  const selectedReceipt = activeReceipts.find((r) => r.id === form.receiptId);

  const openDrawer = () => {
    setForm(EMPTY_MAPPING_FORM);
    setFormError("");
    setDrawerOpen(true);
  };

  const handleSave = async (e) => {
    e.preventDefault();
    if (!form.studentServiceId || !form.receiptId) {
      setFormError(t("treasury_mapping_form_incomplete"));
      return;
    }
    setFormError("");
    try {
      await createMapping.mutateAsync({
        studentServiceId: form.studentServiceId,
        receiptId: form.receiptId,
        quantityMode: Number(form.quantityMode),
      });
      addToast(t("treasury_mapping_created_toast"), "success");
      setDrawerOpen(false);
    } catch (err) {
      setFormError(apiErrorMessage(err, t("treasury_mapping_create_failed")));
    }
  };

  const handleDeactivate = async () => {
    try {
      await deactivateMapping.mutateAsync(deactivateTarget.id);
      addToast(t("treasury_mapping_deactivated_toast"), "info");
    } catch (err) {
      addToast(apiErrorMessage(err, t("treasury_mapping_deactivate_failed")), "error");
    } finally {
      setDeactivateTarget(null);
    }
  };

  const columns = [
    {
      key: "studentServiceId",
      label: t("treasury_service"),
      render: (v) => (
        <span className="ty-cell-strong">
          {serviceById[v]?.name || <span className="ty-code-pill">{String(v).slice(0, 8)}</span>}
        </span>
      ),
    },
    {
      key: "receiptName",
      label: t("treasury_receipt"),
      render: (v) => v || "—",
    },
    {
      key: "unitAmount",
      label: t("treasury_unit_price"),
      align: "right",
      render: (v, row) => (
        <span className="ty-amount-cell">
          {fmtAmount(v)} <span className="ty-currency">{row.currency}</span>
        </span>
      ),
    },
    {
      key: "quantityMode",
      label: t("treasury_quantity_mode"),
      render: (v) => <span className="ty-meta-chip">{t(QUANTITY_MODE_KEYS[v])}</span>,
    },
    {
      key: "isActive",
      label: t("status"),
      render: (v) => (
        <StatusBadge
          status={v ? "active" : "inactive"}
          label={v ? t("active") : t("inactive")}
        />
      ),
    },
    {
      key: "actions",
      label: "",
      align: "right",
      render: (_v, row) =>
        row.isActive ? (
          <PermissionGate resource="payments.transactions" minLevel={2}>
            <button
              className="ty-btn ty-btn-slim ty-btn-danger-ghost"
              onClick={(e) => {
                e.stopPropagation();
                setDeactivateTarget(row);
              }}
            >
              <PowerOff size={12} aria-hidden="true" /> {t("treasury_deactivate")}
            </button>
          </PermissionGate>
        ) : null,
    },
  ];

  return (
    <>
      <div className="ty-toolbar">
        <p className="ty-note ty-note-grow">{t("treasury_mappings_explainer")}</p>
        <PermissionGate resource="payments.transactions" minLevel={2}>
          <button className="ty-btn ty-btn-primary" onClick={openDrawer}>
            <Plus size={14} aria-hidden="true" /> {t("treasury_new_mapping")}
          </button>
        </PermissionGate>
      </div>

      <DataTable
        columns={columns}
        data={mappings}
        loading={isLoading}
        error={error ? apiErrorMessage(error, t("treasury_mappings_load_failed")) : null}
        emptyIcon={Link2}
        emptyTitle={t("treasury_no_mappings_title")}
        emptyMessage={t("treasury_no_mappings_hint")}
        tableLabel={t("treasury_service_mappings_tab")}
      />

      <Drawer
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        title={t("treasury_new_mapping")}
        loading={createMapping.isPending}
        footer={
          <>
            <button
              className="ty-btn"
              onClick={() => setDrawerOpen(false)}
              disabled={createMapping.isPending}
            >
              {t("cancel")}
            </button>
            <button
              className="ty-btn ty-btn-primary"
              onClick={handleSave}
              disabled={createMapping.isPending}
            >
              {createMapping.isPending ? t("saving") : t("treasury_create_mapping")}
            </button>
          </>
        }
      >
        <form className="ty-form" onSubmit={handleSave}>
          <label className="ty-field">
            <span className="ty-field-label">{t("treasury_service")}</span>
            <select
              value={form.studentServiceId}
              onChange={(e) => setForm({ ...form, studentServiceId: e.target.value })}
            >
              <option value="">{t("treasury_select_service")}</option>
              {selectableServices.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                  {s.isPaid && s.price != null ? ` — ${fmtAmount(s.price)}` : ""}
                </option>
              ))}
            </select>
            <span className="ty-field-hint">{t("treasury_service_select_hint")}</span>
          </label>

          <label className="ty-field">
            <span className="ty-field-label">{t("treasury_receipt")}</span>
            <select
              value={form.receiptId}
              onChange={(e) => setForm({ ...form, receiptId: e.target.value })}
            >
              <option value="">{t("treasury_select_receipt")}</option>
              {activeReceipts.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name} — {fmtAmount(r.unitAmount)} {r.currency}
                </option>
              ))}
            </select>
            <span className="ty-field-hint">{t("treasury_receipt_select_hint")}</span>
          </label>

          <fieldset className="ty-field">
            <legend className="ty-field-label">{t("treasury_quantity_mode")}</legend>
            {[QUANTITY_MODE.Fixed, QUANTITY_MODE.CallerSupplied].map((mode) => (
              <label key={mode} className="ty-radio-row">
                <input
                  type="radio"
                  name="quantityMode"
                  checked={Number(form.quantityMode) === mode}
                  onChange={() => setForm({ ...form, quantityMode: mode })}
                />
                <span>
                  <strong>{t(QUANTITY_MODE_KEYS[mode])}</strong>
                  <span className="ty-field-hint">
                    {mode === QUANTITY_MODE.Fixed
                      ? t("treasury_quantity_fixed_hint")
                      : t("treasury_quantity_caller_hint")}
                  </span>
                </span>
              </label>
            ))}
          </fieldset>

          {selectedReceipt && form.studentServiceId && (
            <div className="ty-mapping-preview">
              {t("treasury_mapping_preview", {
                service: serviceById[form.studentServiceId]?.name || "",
                amount: `${fmtAmount(selectedReceipt.unitAmount)} ${selectedReceipt.currency}`,
              })}
              {Number(form.quantityMode) === QUANTITY_MODE.CallerSupplied &&
                ` ${t("treasury_mapping_preview_caller")}`}
            </div>
          )}

          {formError && (
            <div className="ty-alert ty-alert-error" role="alert">
              <AlertCircle size={14} aria-hidden="true" />
              <span>{formError}</span>
            </div>
          )}
        </form>
      </Drawer>

      <ConfirmDialog
        open={!!deactivateTarget}
        onClose={() => setDeactivateTarget(null)}
        onConfirm={handleDeactivate}
        title={t("treasury_deactivate_mapping")}
        message={t("treasury_deactivate_mapping_confirm", {
          service: serviceById[deactivateTarget?.studentServiceId]?.name || t("treasury_service"),
        })}
        detail={t("treasury_deactivate_mapping_detail")}
        confirmLabel={t("treasury_deactivate")}
        cancelLabel={t("keep")}
        variant="warning"
        loading={deactivateMapping.isPending}
      />
    </>
  );
}
