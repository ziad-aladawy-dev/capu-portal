import { useRef, useState } from "react";
import { Camera, Check, Copy, Dices, Key, RefreshCw } from "lucide-react";
import { useToast } from "../../../core/components/Toast";
import "../styles/profilePage.css";

/* ── Copy-to-clipboard ──────────────────────────────────────── */

export function CopyButton({ value, label = "Value" }) {
  const { addToast } = useToast();
  const [copied, setCopied] = useState(false);
  if (!value) return null;
  const copy = async (e) => {
    e.stopPropagation();
    try {
      await navigator.clipboard.writeText(String(value));
      setCopied(true);
      setTimeout(() => setCopied(false), 1400);
    } catch {
      addToast(`Could not copy ${label.toLowerCase()}`, "error");
    }
  };
  return (
    <button type="button" className="pp-copy" onClick={copy} title={`Copy ${label.toLowerCase()}`}>
      {copied ? <Check size={12} /> : <Copy size={12} />}
    </button>
  );
}

/* ── Building blocks ────────────────────────────────────────── */

export function Field({ icon: Icon, label, value, mono, copyable }) {
  const display = value === null || value === undefined || value === "" ? "—" : value;
  return (
    <div className="pp-field">
      {Icon && <div className="pp-field-icon"><Icon size={14} /></div>}
      <div className="pp-field-body">
        <div className="pp-field-label">{label}</div>
        <div className={`pp-field-value ${mono ? "mono" : ""}`}>
          <span style={{ minWidth: 0 }}>{display}</span>
          {copyable && display !== "—" && <CopyButton value={display} label={label} />}
        </div>
      </div>
    </div>
  );
}

export function StatCard({ icon: Icon, label, value, hint, tone = "", onClick }) {
  const Tag = onClick ? "button" : "div";
  return (
    <Tag
      type={onClick ? "button" : undefined}
      className={`pp-stat ${tone ? `tone-${tone}` : ""} ${onClick ? "clickable" : ""}`}
      onClick={onClick}
    >
      <div className="pp-stat-icon"><Icon size={17} /></div>
      <div className="pp-stat-body">
        <div className="pp-stat-value">{value}</div>
        <div className="pp-stat-label">{label}</div>
        {hint && <div className="pp-stat-hint">{hint}</div>}
      </div>
    </Tag>
  );
}

export function TabBar({ tabs, active, onChange }) {
  return (
    <div className="pp-tabs">
      {tabs.map((tab) => {
        const Icon = tab.icon;
        return (
          <button
            key={tab.id}
            type="button"
            className={`pp-tab ${active === tab.id ? "active" : ""}`}
            onClick={() => onChange(tab.id)}
          >
            <Icon size={14} />
            {tab.label}
            {tab.count !== undefined && tab.count !== null && (
              <span className="pp-tab-count">{tab.count}</span>
            )}
          </button>
        );
      })}
    </div>
  );
}

export function Panel({ icon: Icon, title, actions, children, className = "" }) {
  return (
    <div className={`pp-panel ${className}`}>
      {(title || actions) && (
        <div className="pp-panel-head">
          <h3 className="pp-panel-title">{Icon && <Icon size={15} />}{title}</h3>
          {actions && <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>{actions}</div>}
        </div>
      )}
      {children}
    </div>
  );
}

export function EmptyState({ icon: Icon, title, message }) {
  return (
    <div className="pp-empty">
      {Icon && <Icon size={30} />}
      <h4>{title}</h4>
      {message && <p>{message}</p>}
    </div>
  );
}

/* ── Hero ───────────────────────────────────────────────────── */

export function ProfileHero({
  photoUrl, initial, name, subtitle, badges, chips, actions,
  onUploadPhoto, uploading, validating, validationOverlay,
}) {
  const fileRef = useRef(null);
  const { addToast } = useToast();

  const pickPhoto = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) {
      addToast("Photo must be JPEG, PNG or WebP", "error");
    } else if (file.size > 5 * 1024 * 1024) {
      addToast("Photo must be under 5 MB", "error");
    } else {
      onUploadPhoto(file);
    }
    e.target.value = "";
  };

  return (
    <div className="pp-hero pp-fade">
      <div className="pp-avatar-wrap">
        {photoUrl
          ? <img src={photoUrl} alt={name} className="pp-photo" />
          : <div className="pp-avatar">{initial}</div>}
        {onUploadPhoto && (
          <>
            <button
              type="button"
              className="pp-photo-btn"
              onClick={() => fileRef.current?.click()}
              disabled={uploading || validating}
              title="Upload photo"
            >
              {uploading || validating ? <RefreshCw size={13} className="pp-spin" /> : <Camera size={13} />}
            </button>
            <input
              ref={fileRef}
              type="file"
              accept="image/jpeg,image/png,image/webp"
              style={{ display: "none" }}
              onChange={pickPhoto}
            />
          </>
        )}
        {validationOverlay}
      </div>

      <div className="pp-hero-main">
        <h1 className="pp-hero-name">{name}</h1>
        {subtitle && <div className="pp-hero-sub">{subtitle}</div>}
        {badges && <div className="pp-hero-badges">{badges}</div>}
        {chips && <div className="pp-hero-chips">{chips}</div>}
      </div>

      {actions && <div className="pp-hero-actions">{actions}</div>}
    </div>
  );
}

/* ── Reset password modal ───────────────────────────────────── */

const PASSWORD_CHARS = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%";

function generatePassword(length = 12) {
  const buf = new Uint32Array(length);
  crypto.getRandomValues(buf);
  return Array.from(buf, (n) => PASSWORD_CHARS[n % PASSWORD_CHARS.length]).join("");
}

/**
 * Sets a new password for the user. `onSubmit(password)` should resolve when
 * the backend accepted the change (full entity payload + password fields).
 */
export function ResetPasswordModal({ open, onClose, userName, onSubmit, pending }) {
  const { addToast } = useToast();
  const [password, setPassword] = useState("");

  if (!open) return null;

  const close = () => {
    if (pending) return;
    setPassword("");
    onClose();
  };

  const roll = () => setPassword(generatePassword());

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(password);
      addToast("Password copied to clipboard", "success");
    } catch {
      addToast("Could not copy password", "error");
    }
  };

  const submit = async () => {
    if (password.length < 8) {
      addToast("Password must be at least 8 characters", "error");
      return;
    }
    await onSubmit(password);
    setPassword("");
  };

  return (
    <div className="pp-modal-backdrop" onClick={close}>
      <div className="pp-modal" onClick={(e) => e.stopPropagation()}>
        <h3><Key size={15} /> Reset Password</h3>
        <p className="pp-modal-sub">
          Set a new password for <strong>{userName}</strong>. Share it through a secure channel —
          it will not be shown again.
        </p>
        <div className="pp-password-row">
          <input
            type="text"
            className="pp-input"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            placeholder="New password (min 8 characters)"
            autoFocus
            spellCheck={false}
            style={{ fontFamily: "Space Mono, monospace" }}
          />
          <button type="button" className="pp-btn soft" onClick={roll} title="Generate a strong password">
            <Dices size={14} />
          </button>
          <button type="button" className="pp-btn soft" onClick={copy} disabled={!password} title="Copy password">
            <Copy size={14} />
          </button>
        </div>
        <div className="pp-modal-actions">
          <button type="button" className="pp-btn soft" onClick={close} disabled={pending}>Cancel</button>
          <button type="button" className="pp-btn navy" onClick={submit} disabled={pending || !password}>
            {pending ? "Saving…" : "Set Password"}
          </button>
        </div>
      </div>
    </div>
  );
}
