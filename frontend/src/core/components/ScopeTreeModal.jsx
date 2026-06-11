import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { X, ChevronRight, ChevronDown } from "lucide-react";
import { fetchStructureTree } from "../services/structureService";
import { getLocalized } from "../utils/getLocalized";
import { getNodeTypeConfig } from "../constants/nodeTypeRegistry";
import NodeTypeBadge from "./NodeTypeBadge";
import "./scopeTreeModal.css";

function TreeNode({ node, onSelect, selectedId, level = 0 }) {
  const { i18n } = useTranslation();
  const [open, setOpen] = useState(level < 1);
  const hasChildren = node.children && node.children.length > 0;
  const typeConfig = getNodeTypeConfig(node.type);
  const Icon = typeConfig?.icon || null;
  const typeColor = typeConfig?.color || "var(--color-text-secondary)";
  const isSelected = selectedId === node.id;
  const displayName = node.localizedName || getLocalized(node.name, i18n.language);

  return (
    <div className="stm-node">
      <div
        className={`stm-item ${isSelected ? "selected" : ""}`}
        style={{ paddingInlineStart: `${level * 20 + 12}px` }}
        onClick={() => onSelect(node)}
      >
        {hasChildren && (
          <button
            className="stm-toggle"
            onClick={(e) => {
              e.stopPropagation();
              setOpen(!open);
            }}
          >
            {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          </button>
        )}
        {!hasChildren && <span className="stm-placeholder" />}
        {Icon && (
          <div className="stm-icon" style={{ color: typeColor }}>
            <Icon size={14} />
          </div>
        )}
        <span className="stm-name">{displayName}</span>
        <NodeTypeBadge type={node.type} size="xs" />
      </div>
      {hasChildren && open && (
        <div className="stm-children">
          {node.children.map((child) => (
            <TreeNode
              key={child.id}
              node={child}
              onSelect={onSelect}
              selectedId={selectedId}
              level={level + 1}
            />
          ))}
        </div>
      )}
    </div>
  );
}

export function ScopeTreeModal({ isOpen, onClose, onSelect, initialScopeId }) {
  const { t, i18n } = useTranslation();
  const [tree, setTree] = useState([]);
  const [loading, setLoading] = useState(false);
  const [selectedId, setSelectedId] = useState(initialScopeId);

  useEffect(() => {
    if (!isOpen) return;
    let cancelled = false;
    setLoading(true);
    fetchStructureTree()
      .then((data) => {
        if (!cancelled) setTree(data);
      })
      .catch((err) => console.error(err))
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [isOpen, i18n.language]);

  const handleSelect = (node) => {
    setSelectedId(node.id);
    const localizedName = node.localizedName || getLocalized(node.name, i18n.language);
    onSelect({
      id: node.id,
      name: node.name,
      originalName: node.name,
      localizedName: localizedName,
      type: node.type,
      path: node.path,
    });
    onClose();
  };

  if (!isOpen) return null;

  return (
    <div className="stm-overlay" onClick={onClose}>
      <div className="stm-container" onClick={(e) => e.stopPropagation()}>
        <div className="stm-header">
          <h3>{t("select_scope")}</h3>
          <button className="stm-close" onClick={onClose}><X size={18} /></button>
        </div>
        <div className="stm-body">
          {loading ? (
            <div className="stm-loading">{t("loading")}...</div>
          ) : tree.length === 0 ? (
            <div className="stm-empty">{t("no_structure")}</div>
          ) : (
            tree.map((root) => (
              <TreeNode
                key={`${root.id}-${i18n.language}`}
                node={root}
                onSelect={handleSelect}
                selectedId={selectedId}
              />
            ))
          )}
        </div>
      </div>
    </div>
  );
}
