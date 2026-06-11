import { useState, useEffect, useCallback, useRef } from "react";
import {
  X, ChevronRight, ChevronDown, Building2, Check,
  CalendarRange, BookOpen, Globe, Trash2, Search
} from "lucide-react";
import { useTranslation } from "react-i18next";
import * as structureService from "../services/structureService";
import { getLocalized } from "../utils/getLocalized";
import { useDomain } from "../contexts/DomainContext";
import { useAcademic } from "../contexts/AcademicContext";
import { getNodeTypeConfig } from "../constants/nodeTypeRegistry";
import NodeTypeBadge from "./NodeTypeBadge";
import "./scopeModal.css";

function formatDate(iso) {
  if (!iso) return "";
  const d = new Date(iso);
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

function TreeNode({ node, expandedNodes, toggleNode, selectedId, onSelect, depth }) {
  const { i18n } = useTranslation();
  const hasChildren = node.children && node.children.length > 0;
  const isExpanded = expandedNodes.has(node.id);
  const isSelected = selectedId === node.id;
  const typeConfig = getNodeTypeConfig(node.type);
  const Icon = typeConfig?.icon || Building2;
  const typeColor = typeConfig?.color || "rgba(26, 31, 94, 0.4)";
  const displayName = node.localizedName || getLocalized(node.name, i18n.language);

  return (
    <div className="scope-tree-group">
      <button
        className={`scope-tree-node ${isSelected ? "is-selected" : ""}`}
        onClick={() => onSelect(node)}
        style={{ paddingLeft: 12 + depth * 16 }}
      >
        {hasChildren ? (
          <span
            className="scope-tree-toggle"
            onClick={(e) => { e.stopPropagation(); toggleNode(node.id); }}
          >
            {isExpanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
          </span>
        ) : (
          <span className="scope-tree-toggle scope-tree-toggle-placeholder" />
        )}
        <Icon size={14} className="scope-tree-type-icon" style={{ color: typeColor }} />
        <span className="scope-tree-label">{displayName}</span>
        <NodeTypeBadge type={node.type} size="xs" />
        {isSelected && <Check size={12} className="scope-tree-check" />}
      </button>
      {hasChildren && isExpanded && (
        <div className="scope-tree-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              expandedNodes={expandedNodes}
              toggleNode={toggleNode}
              selectedId={selectedId}
              onSelect={onSelect}
              depth={depth + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
}

function TemporalDropdown({ label, icon: Icon, items, selected, onSelect, onClear, loading, emptyText, allLabel }) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const searchRef = useRef(null);

  useEffect(() => {
    if (open && searchRef.current) {
      searchRef.current.focus();
      setSearchQuery("");
    }
  }, [open]);

  const filteredItems = items.filter((item) => {
    if (!searchQuery) return true;
    const name = getLocalized(item.name, i18n.language).toLowerCase();
    const query = searchQuery.toLowerCase();
    return name.includes(query);
  });

  return (
    <div className="scope-temporal-group">
      <span className="scope-temporal-label">
        <Icon size={12} />
        {label}
      </span>
      <div className="scope-temporal-wrapper">
        <button
          className={`scope-temporal-trigger ${open ? "is-open" : ""} ${selected ? "has-active" : ""}`}
          onClick={() => setOpen(!open)}
        >
          {selected ? (
            <div className="scope-temporal-trigger-text">
              <strong>{getLocalized(selected.name, i18n.language)}</strong>
              <span className="scope-temporal-trigger-sub">
                {formatDate(selected.startDate)} – {formatDate(selected.endDate)}
              </span>
            </div>
          ) : (
            <span className="scope-temporal-placeholder">{allLabel || t("all_scopes")}</span>
          )}
          <ChevronDown size={12} />
        </button>
        {open && (
          <>
            <div className="scope-temporal-backdrop" onClick={() => setOpen(false)} />
            <div className="scope-temporal-dropdown">
              <div className="scope-temporal-search">
                <Search size={13} />
                <input
                  ref={searchRef}
                  type="text"
                  placeholder={t("search") + "..."}
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  onClick={(e) => e.stopPropagation()}
                />
                {searchQuery && (
                  <button className="scope-temporal-search-clear" onClick={() => setSearchQuery("")}>
                    <X size={11} />
                  </button>
                )}
              </div>
              <div className="scope-temporal-divider" />
              {!searchQuery && (
                <button
                  className={`scope-temporal-item ${!selected ? "is-selected" : ""}`}
                  onClick={() => { onClear(); setOpen(false); }}
                >
                  <Globe size={13} />
                  <span>{allLabel || t("all_scopes")}</span>
                  {!selected && <Check size={11} className="scope-temporal-check" />}
                </button>
              )}
              {loading ? (
                <div className="scope-temporal-item" style={{ justifyContent: "center", opacity: 0.5 }}>
                  {t("loading")}…
                </div>
              ) : filteredItems.length === 0 ? (
                <div className="scope-temporal-item" style={{ justifyContent: "center", opacity: 0.5 }}>
                  {searchQuery ? t("no_results") : emptyText}
                </div>
              ) : filteredItems.map((item) => (
                <button
                  key={item.id}
                  className={`scope-temporal-item ${selected?.id === item.id ? "is-selected" : ""} ${item.isClosed ? "is-closed" : ""}`}
                  onClick={() => { onSelect(item); setOpen(false); }}
                >
                  <Icon size={13} />
                  <div className="scope-temporal-item-text">
                    <strong>{getLocalized(item.name, i18n.language)}</strong>
                    <span className="scope-temporal-item-dates">
                      {formatDate(item.startDate)} – {formatDate(item.endDate)}
                    </span>
                  </div>
                  <div className="scope-temporal-item-tags">
                    {item.isCurrent && <span className="scope-current-badge">{t("current_badge")}</span>}
                    {item.isClosed && <span className="scope-closed-badge">{t("closed_badge")}</span>}
                  </div>
                  {selected?.id === item.id && <Check size={11} className="scope-temporal-check" />}
                </button>
              ))}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function ScopeModal({ onClose }) {
  const { t, i18n } = useTranslation();
  const { scopeNode, selectScopeNode } = useDomain();
  const {
    academicYears, semesters, selectedYearObj, selectedSemesterObj,
    selectYear, selectSemester,
    loading: academicLoading,
  } = useAcademic();

  const [tree, setTree] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedNodes, setExpandedNodes] = useState(new Set());
  const [selectedNode, setSelectedNode] = useState(null);
  const [tempYear, setTempYear] = useState(null);
  const [tempSemester, setTempSemester] = useState(null);

  useEffect(() => {
    setLoading(true);
    structureService.fetchStructureTree()
      .then((data) => {
        const roots = Array.isArray(data) ? data : [];
        setTree(roots);
        if (roots.length > 0) {
          setExpandedNodes((prev) => new Set(prev).add(roots[0].id));
        }
      })
      .catch(() => setTree([]))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    if (scopeNode) setSelectedNode(scopeNode);
  }, [scopeNode]);

  useEffect(() => {
    setTempYear(selectedYearObj || null);
  }, [selectedYearObj]);

  useEffect(() => {
    setTempSemester(selectedSemesterObj || null);
  }, [selectedSemesterObj]);

  const toggleNode = useCallback((nodeId) => {
    setExpandedNodes((prev) => {
      const next = new Set(prev);
      if (next.has(nodeId)) next.delete(nodeId);
      else next.add(nodeId);
      return next;
    });
  }, []);

  const handleSelect = useCallback((node) => {
    setSelectedNode(node);
  }, []);

  const handleApply = useCallback(() => {
    selectScopeNode(selectedNode);
    selectYear(tempYear);
    selectSemester(tempSemester);
    onClose();
  }, [selectedNode, tempYear, tempSemester, selectScopeNode, selectYear, selectSemester, onClose]);

  const handleClearAll = useCallback(() => {
    setSelectedNode(null);
    setTempYear(null);
    setTempSemester(null);
  }, []);

  const hasAnySelection = selectedNode || tempYear || tempSemester;

  return (
    <div className="scope-modal-overlay" onClick={onClose}>
      <div className="scope-modal" onClick={(e) => e.stopPropagation()}>
        <div className="scope-modal-header">
          <h2>{t("select_scope")}</h2>
          <button className="scope-modal-close" onClick={onClose}>
            <X size={16} />
          </button>
        </div>

        <div className="scope-modal-body">
          <div className="scope-modal-tree-panel">
            <div className="scope-modal-panel-label">{t("university_structure")}</div>
            {loading ? (
              <div className="scope-modal-loading">{t("loading")}…</div>
            ) : tree.length === 0 ? (
              <div className="scope-modal-loading">{t("no_data")}</div>
            ) : (
              <div className="scope-tree-scroll">
                {tree.map((root) => (
                  <TreeNode
                    key={root.id}
                    node={root}
                    expandedNodes={expandedNodes}
                    toggleNode={toggleNode}
                    selectedId={selectedNode?.id}
                    onSelect={handleSelect}
                    depth={0}
                  />
                ))}
              </div>
            )}
          </div>

          <div className="scope-modal-temporal-panel">
            <div className="scope-modal-panel-label">{t("temporal_scope")}</div>

            <TemporalDropdown
              label={t("academic_year_scope")}
              icon={CalendarRange}
              items={academicYears}
              selected={tempYear}
              onSelect={setTempYear}
              onClear={() => { setTempYear(null); setTempSemester(null); }}
              loading={academicLoading}
              emptyText={t("no_years_found")}
              allLabel={t("all_years")}
            />
            <TemporalDropdown
              label={t("semester_scope")}
              icon={BookOpen}
              items={semesters}
              selected={tempSemester}
              onSelect={setTempSemester}
              onClear={() => setTempSemester(null)}
              loading={academicLoading}
              emptyText={t("no_semesters")}
              allLabel={t("all_semesters")}
            />

            <button
              className={`scope-clear-all ${!hasAnySelection ? "is-disabled" : ""}`}
              onClick={handleClearAll}
              disabled={!hasAnySelection}
            >
              <Trash2 size={13} />
              <span>{t("clear_all_scope")}</span>
            </button>
          </div>
        </div>

        <div className="scope-modal-footer">
          <button className="scope-btn scope-btn-secondary" onClick={onClose}>
            {t("cancel")}
          </button>
          <button
            className="scope-btn scope-btn-primary"
            onClick={handleApply}
          >
            {t("apply")}
          </button>
        </div>
      </div>
    </div>
  );
}

export default ScopeModal;
