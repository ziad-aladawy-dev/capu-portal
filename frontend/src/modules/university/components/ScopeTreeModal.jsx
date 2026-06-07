import { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { X, ChevronRight, ChevronDown, Building2, GraduationCap, Layers, BookOpen, Award } from "lucide-react";
import { universityStructureService } from "../services/universityStructureService";
import { getLocalized } from "../../../core/utils/getLocalized";
import "../styles/scopeModal.css";

const typeIcons = {
  University: Building2,
  Faculty: GraduationCap,
  Department: Layers,
  Program: BookOpen,
  Level: Award,
  System: Layers,
  Specialization: Award,
};

function TreeNode({ node, onSelect, selectedId, level = 0 }) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(level < 1);
  const hasChildren = node.children && node.children.length > 0;
  const Icon = typeIcons[node.type] || Building2;
  const isSelected = selectedId === node.id;
  const displayName = node.localizedName || getLocalized(node.name, i18n.language);

  return (
    <div className="scope-tree-node">
      <div
        className={`scope-tree-item ${isSelected ? "selected" : ""}`}
        style={{ paddingLeft: `${level * 20 + 12}px` }}
        onClick={() => onSelect(node)}
      >
        {hasChildren && (
          <button
            className="scope-tree-toggle"
            onClick={(e) => {
              e.stopPropagation();
              setOpen(!open);
            }}
          >
            {open ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
          </button>
        )}
        {!hasChildren && <span className="scope-tree-placeholder" />}
        <div className="scope-tree-icon"><Icon size={14} /></div>
        <span className="scope-tree-name">{displayName}</span>
        <span className="scope-tree-type">{node.type}</span>
      </div>
      {hasChildren && open && (
        <div className="scope-tree-children">
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

  const fetchTreeData = async () => {
    setLoading(true);
    try {
      const data = await universityStructureService.getTree();
      setTree(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchTreeData();
    }
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
    <div className="scope-modal-overlay" onClick={onClose}>
      <div className="scope-modal-container" onClick={(e) => e.stopPropagation()}>
        <div className="scope-modal-header">
          <h3>{t("select_scope")}</h3>
          <button className="scope-modal-close" onClick={onClose}><X size={18} /></button>
        </div>
        <div className="scope-modal-body">
          {loading ? (
            <div className="scope-loading">{t("loading")}...</div>
          ) : tree.length === 0 ? (
            <div className="scope-empty">{t("no_structure")}</div>
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