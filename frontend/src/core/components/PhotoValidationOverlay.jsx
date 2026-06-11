import { useRef, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  CheckCircle, XCircle, AlertTriangle, Camera, RefreshCw, Loader2,
} from "lucide-react";

const GROUP_ORDER = ["file", "face", "quality"];
const GROUP_LABELS = {
  file: "photo_group_file",
  face: "photo_group_face",
  quality: "photo_group_quality",
};

function getCheckIcon(passed, blocking) {
  if (passed) return CheckCircle;
  return blocking ? XCircle : AlertTriangle;
}

function getCheckColor(passed, blocking) {
  if (passed) return "#16a34a";
  return blocking ? "#dc2626" : "#d97706";
}

function PhotoValidationOverlay({
  results,
  previewUrl,
  isProcessing,
  error,
  onAccept,
  onReject,
  onRetry,
}) {
  const { t } = useTranslation();
  const [exiting, setExiting] = useState(false);
  const overlayRef = useRef(null);

  useEffect(() => {
    const handleKey = (e) => {
      if (e.key === "Escape") {
        if (!results || results.hasBlockingFails) return;
        handleAccept();
      }
    };
    window.addEventListener("keydown", handleKey);
    return () => window.removeEventListener("keydown", handleKey);
  }, [results]);

  const handleAccept = () => {
    if (exiting) return;
    setExiting(true);
    setTimeout(() => {
      setExiting(false);
      onAccept?.();
    }, 200);
  };

  const handleReject = () => {
    if (exiting) return;
    setExiting(true);
    setTimeout(() => {
      setExiting(false);
      onReject?.();
    }, 200);
  };

  const canProceed = results && !results.hasBlockingFails;

  const grouped = {};
  if (results?.checks) {
    for (const check of results.checks) {
      const g = check.group || "file";
      if (!grouped[g]) grouped[g] = [];
      grouped[g].push(check);
    }
  }

  return (
    <div
      ref={overlayRef}
      className={`pv-overlay ${exiting ? "is-exiting" : ""}`}
      onClick={(e) => {
        if (e.target === overlayRef.current && !results?.hasBlockingFails) {
          handleReject();
        }
      }}
    >
      <div className="pv-modal" onClick={(e) => e.stopPropagation()}>
        <div className="pv-header">
          <Camera size={18} />
          <span>{t("photo_validation")}</span>
        </div>

        <div className="pv-body">
          <div className="pv-preview-col">
            {previewUrl ? (
              <div className="pv-preview-wrap">
                <img src={previewUrl} alt="Preview" className="pv-preview" />
              </div>
            ) : (
              <div className="pv-preview-placeholder">
                <Camera size={32} />
              </div>
            )}

            {results && (
              <div className="pv-score">
                <div
                  className="pv-score-ring"
                  style={{
                    background: results.score >= 80
                      ? "conic-gradient(#16a34a 0% 100%)"
                      : results.score >= 50
                      ? `conic-gradient(#d97706 0% ${results.score}%, #e5e7eb ${results.score}% 100%)`
                      : `conic-gradient(#dc2626 0% ${results.score}%, #e5e7eb ${results.score}% 100%)`,
                  }}
                >
                  <div className="pv-score-inner">
                    <span className="pv-score-value">{results.score}</span>
                    <span className="pv-score-label">{t("photo_score")}</span>
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="pv-checks-col">
            {isProcessing && (
              <div className="pv-processing">
                <Loader2 size={20} className="pv-spin" />
                <span>{t("photo_validating")}</span>
              </div>
            )}

            {error && (
              <div className="pv-error">
                <AlertTriangle size={14} />
                <span>{error}</span>
              </div>
            )}

            {results && GROUP_ORDER.map((groupKey) => {
              const items = grouped[groupKey];
              if (!items || items.length === 0) return null;
              return (
                <div key={groupKey} className="pv-group">
                  <div className="pv-group-title">{t(GROUP_LABELS[groupKey])}</div>
                  <div className="pv-checks">
                    {items.map((check) => {
                      const Icon = getCheckIcon(check.passed, check.blocking);
                      const color = getCheckColor(check.passed, check.blocking);
                      return (
                        <div key={check.key} className={`pv-check ${check.passed ? "is-pass" : check.blocking ? "is-fail" : "is-warn"}`}>
                          <Icon size={14} style={{ color, flexShrink: 0 }} />
                          <span className="pv-check-label">{t(check.labelKey)}</span>
                          {check.detail && (
                            <span className="pv-check-detail">{check.detail}</span>
                          )}
                        </div>
                      );
                    })}
                  </div>
                </div>
              );
            })}

            {results && !results.passed && !results.hasBlockingFails && (
              <div className="pv-advisory">
                <AlertTriangle size={13} />
                <span>{t("photo_advisory_note")}</span>
              </div>
            )}

            {results && results.hasBlockingFails && (
              <div className="pv-blocking-note">
                <XCircle size={13} />
                <span>{t("photo_blocking_note")}</span>
              </div>
            )}
          </div>
        </div>

        {!isProcessing && results && (
          <div className="pv-footer">
            {canProceed ? (
              <>
                <button type="button" className="pv-btn pv-btn-secondary" onClick={handleReject}>
                  {t("photo_retake")}
                </button>
                <button type="button" className="pv-btn pv-btn-primary" onClick={handleAccept}>
                  <CheckCircle size={14} />
                  {t("photo_use_photo")}
                </button>
              </>
            ) : (
              <>
                <button type="button" className="pv-btn pv-btn-primary" onClick={handleReject}>
                  {t("photo_choose_another")}
                </button>
                {onRetry && (
                  <button type="button" className="pv-btn pv-btn-secondary" onClick={onRetry}>
                    <RefreshCw size={13} />
                    {t("retry")}
                  </button>
                )}
              </>
            )}
          </div>
        )}

        {isProcessing && (
          <div className="pv-footer">
            <div className="pv-processing-inline">
              <Loader2 size={14} className="pv-spin" />
              <span>{t("photo_validating")}</span>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export {
  PhotoValidationOverlay,
  getCheckIcon,
  getCheckColor,
  GROUP_ORDER,
  GROUP_LABELS,
};
export default PhotoValidationOverlay;
