import { useEffect } from "react";
import { X } from "lucide-react";

const SHORTCUT_GROUPS = [
  {
    title: "Global",
    shortcuts: [
      { keys: ["Ctrl", "K"], desc: "Open command palette" },
      { keys: ["?"], desc: "Show keyboard shortcuts" },
      { keys: ["Esc"], desc: "Close modal / cancel" },
    ],
  },
  {
    title: "Navigation",
    shortcuts: [
      { keys: ["Tab"], desc: "Move to next field" },
      { keys: ["Shift", "Tab"], desc: "Move to previous field" },
      { keys: ["Enter"], desc: "Submit form / confirm" },
    ],
  },
  {
    title: "Tables",
    shortcuts: [
      { keys: ["↑", "↓"], desc: "Navigate rows" },
      { keys: ["Enter"], desc: "Open selected item" },
    ],
  },
];

function KeyboardShortcutsModal({ onClose }) {
  useEffect(() => {
    const handler = (e) => {
      if (e.key === "Escape") onClose();
    };
    document.addEventListener("keydown", handler);
    return () => document.removeEventListener("keydown", handler);
  }, [onClose]);

  return (
    <div className="kbd-modal-overlay" onClick={onClose}>
      <div className="kbd-modal" onClick={(e) => e.stopPropagation()}>
        <div className="kbd-modal-header">
          <h2>Keyboard Shortcuts</h2>
          <button className="kbd-modal-close" onClick={onClose}>
            <X size={16} />
          </button>
        </div>
        <div className="kbd-modal-body">
          {SHORTCUT_GROUPS.map((group) => (
            <div key={group.title} className="kbd-group">
              <h3 className="kbd-group-title">{group.title}</h3>
              {group.shortcuts.map((s, idx) => (
                <div key={idx} className="kbd-row">
                  <span className="kbd-desc">{s.desc}</span>
                  <span className="kbd-keys">
                    {s.keys.map((k, ki) => (
                      <span key={ki}>
                        {ki > 0 && <span className="kbd-plus">+</span>}
                        <kbd>{k}</kbd>
                      </span>
                    ))}
                  </span>
                </div>
              ))}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default KeyboardShortcutsModal;
