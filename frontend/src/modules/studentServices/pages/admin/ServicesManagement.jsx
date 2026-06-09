import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Plus, Edit3, Copy, Trash2, Power, Search } from "lucide-react";
import { useServices } from "../../hooks/useServices";
import { createService } from "../../services/studentServicesService";
import PermissionGate from "../../../../core/auth/PermissionGate";
import LoadingSpinner from "../../components/LoadingSpinner";
import EmptyState from "../../components/EmptyState";
import StatusBadge from "../../components/StatusBadge";
import "../../styles/admin/ServicesManagement.css";

const ServicesManagement = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { services, loading, removeService, toggleStatus, refresh } = useServices();
  const [search, setSearch] = useState("");

  const handleDuplicate = async (service) => {
    const { id, ...rest } = service;
    const newService = {
      ...rest,
      name: `${rest.name} (Copy)`,
      isActive: false,
      workflow: rest.workflow ? { ...rest.workflow, steps: rest.workflow.steps.map(s => ({ ...s, id: undefined })) } : { steps: [] }
    };
    await createService(newService);
    await refresh();
  };

  const filteredServices = (services || []).filter(s =>
    s.name.toLowerCase().includes(search.toLowerCase())
  );

  if (loading && services.length === 0) return <LoadingSpinner />;

  return (
    <div className="services-management-page">
      <div className="services-header">
        <h1>{t("services_management")}</h1>
        <PermissionGate resource="student-services.services" minLevel={2}>
          <button className="btn-create" onClick={() => navigate("/admin/student-services/services/create")}>
            <Plus size={16} /> {t("create_service")}
          </button>
        </PermissionGate>
      </div>
      <div className="services-filters">
        <div className="search-box">
          <Search size={16} />
          <input type="text" placeholder={t("search_services")} value={search} onChange={e => setSearch(e.target.value)} />
        </div>
      </div>
      {filteredServices.length === 0 ? (
        <EmptyState message={t("no_services")} />
      ) : (
        <div className="services-table-wrapper">
          <table className="services-table">
            <thead>
              <tr>
                <th>{t("service_name")}</th><th>{t("type")}</th><th>{t("status")}</th>
                <th>{t("pricing")}</th><th>{t("actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filteredServices.map(s => (
                <tr key={s.id}>
                  <td><strong>{s.name}</strong><br /><small>{s.description}</small></td>
                  <td>{s.type === "General" ? "عامة" : s.type === "Specialized" ? "متخصصة" : "إدارية"}</td>
                  <td><StatusBadge status={s.isActive ? "active" : "inactive"} type="service" /></td>
                  <td>{s.isPaid ? `$${s.price}` : t("free")}</td>
                  <td className="actions-cell">
                    <PermissionGate resource="student-services.services" minLevel={3}>
                      <button className="action-btn edit" onClick={() => navigate(`/admin/student-services/services/edit/${s.id}`)} title="Edit"><Edit3 size={16} /></button>
                    </PermissionGate>
                    <PermissionGate resource="student-services.services" minLevel={2}>
                      <button className="action-btn duplicate" onClick={() => handleDuplicate(s)} title="Duplicate"><Copy size={16} /></button>
                    </PermissionGate>
                    {s.isActive ? (
                      <PermissionGate resource="student-services.services" minLevel={3}>
                        <button className="action-btn toggle" onClick={() => toggleStatus(s.id)} title="Deactivate"><Power size={16} /></button>
                      </PermissionGate>
                    ) : (
                      <PermissionGate resource="student-services.services" minLevel={4}>
                        <button className="action-btn toggle inactive" onClick={() => toggleStatus(s.id)} title="Activate"><Power size={16} /></button>
                      </PermissionGate>
                    )}
                    <PermissionGate resource="student-services.services" minLevel={5}>
                      <button className="action-btn delete" onClick={() => removeService(s.id)} title="Delete"><Trash2 size={16} /></button>
                    </PermissionGate>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

export default ServicesManagement;