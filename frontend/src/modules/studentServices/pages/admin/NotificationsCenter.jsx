import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Bell, CheckCheck, Trash2, Info, AlertCircle, CheckCircle } from "lucide-react";
import apiClient from "../../../../core/api/apiClient";
import LoadingSpinner from "../../components/LoadingSpinner";
import "../../styles/admin/NotificationsCenter.css";

const NotificationsCenter = () => {
  const { t } = useTranslation(); const [notifications, setNotifications] = useState([]); const [loading, setLoading] = useState(true); const [filter, setFilter] = useState("all");
  useEffect(() => { loadNotifications(); }, []);
  const loadNotifications = async () => { setLoading(true); try { const response = await apiClient.get("/api/notifications"); setNotifications(response.data.items || []); } catch (err) { console.error("Failed to load notifications", err); } finally { setLoading(false); } };
  const markAsRead = async (id) => { try { await apiClient.put(`/api/notifications/${id}/read`); await loadNotifications(); } catch (err) {} };
  const markAllAsRead = async () => { try { await apiClient.put("/api/notifications/read", { ids: notifications.filter(n=>!n.isRead).map(n=>n.id) }); await loadNotifications(); } catch (err) {} };
  const deleteNotification = async (id) => { try { await apiClient.delete(`/api/notifications/${id}`); await loadNotifications(); } catch { setNotifications(prev => prev.filter(n=>n.id!==id)); } };
  const filtered = notifications.filter(n => { if (filter==="unread") return !n.isRead; if (filter==="read") return n.isRead; return true; });
  const getIcon = (type) => { if (type==="Success") return <CheckCircle size={18} />; if (type==="Warning") return <AlertCircle size={18} />; return <Info size={18} />; };
  if (loading) return <LoadingSpinner />;
  return (
    <div className="nc-container"><div className="nc-header"><div className="nc-title"><Bell size={20} /><h1>{t("notifications")}</h1><span className="nc-badge">{notifications.filter(n=>!n.isRead).length}</span></div><button className="nc-mark-all" onClick={markAllAsRead}><CheckCheck size={16} /> {t("mark_all_read")}</button></div>
    <div className="nc-filters"><button className={filter==="all"?"active":""} onClick={()=>setFilter("all")}>{t("all")} ({notifications.length})</button><button className={filter==="unread"?"active":""} onClick={()=>setFilter("unread")}>{t("unread")} ({notifications.filter(n=>!n.isRead).length})</button><button className={filter==="read"?"active":""} onClick={()=>setFilter("read")}>{t("read")} ({notifications.filter(n=>n.isRead).length})</button></div>
    <div className="nc-list">{filtered.map(notif => (<div key={notif.id} className={`nc-item ${!notif.isRead?"nc-unread":""}`}><div className="nc-icon">{getIcon(notif.type)}</div><div className="nc-content"><h4>{notif.title}</h4><p>{notif.message}</p><span>{new Date(notif.createdAt).toLocaleString()}</span></div><div className="nc-actions-buttons">{!notif.isRead && (<button className="nc-read-btn" onClick={()=>markAsRead(notif.id)}><CheckCheck size={16} /></button>)}<button className="nc-delete-btn" onClick={()=>deleteNotification(notif.id)}><Trash2 size={16} /></button></div></div>))}</div></div>
  );
};

export default NotificationsCenter;