import { useState, useEffect, useRef, useCallback } from "react";
import { Clock, X } from "lucide-react";
import api from "../api/apiClient";

const TIMEOUT_MS = 30 * 60 * 1000;      // 30 minutes total session
const WARNING_BEFORE_MS = 2 * 60 * 1000; // Show warning 2 minutes before
const WARNING_AT_MS = TIMEOUT_MS - WARNING_BEFORE_MS; // 28 minutes

/**
 * Tracks user activity and shows a session timeout warning
 * 2 minutes before auto-logout. "Stay logged in" refreshes the token.
 */
function SessionTimeoutWarning({ onLogout }) {
  const [showWarning, setShowWarning] = useState(false);
  const [remaining, setRemaining] = useState(WARNING_BEFORE_MS / 1000);
  const idleTimerRef = useRef(null);
  const countdownRef = useRef(null);
  const lastActivityRef = useRef(Date.now());

  const resetIdleTimer = useCallback(() => {
    lastActivityRef.current = Date.now();

    // If warning is showing, dismiss it
    if (showWarning) {
      setShowWarning(false);
      if (countdownRef.current) clearInterval(countdownRef.current);
    }
  }, [showWarning]);

  // Track user activity
  useEffect(() => {
    const events = ["mousedown", "keydown", "scroll", "touchstart"];
    const handler = () => { lastActivityRef.current = Date.now(); };
    events.forEach((e) => document.addEventListener(e, handler, { passive: true }));
    return () => events.forEach((e) => document.removeEventListener(e, handler));
  }, []);

  // Check idle state every 30 seconds
  useEffect(() => {
    idleTimerRef.current = setInterval(() => {
      const idleTime = Date.now() - lastActivityRef.current;

      if (idleTime >= TIMEOUT_MS) {
        // Session expired
        clearInterval(idleTimerRef.current);
        if (countdownRef.current) clearInterval(countdownRef.current);
        onLogout?.();
        return;
      }

      if (idleTime >= WARNING_AT_MS && !showWarning) {
        // Show warning
        setShowWarning(true);
        const expireAt = lastActivityRef.current + TIMEOUT_MS;
        setRemaining(Math.max(0, Math.floor((expireAt - Date.now()) / 1000)));

        countdownRef.current = setInterval(() => {
          const left = Math.max(0, Math.floor((expireAt - Date.now()) / 1000));
          setRemaining(left);
          if (left <= 0) {
            clearInterval(countdownRef.current);
            onLogout?.();
          }
        }, 1000);
      }
    }, 30000);

    return () => {
      clearInterval(idleTimerRef.current);
      if (countdownRef.current) clearInterval(countdownRef.current);
    };
  }, [onLogout, showWarning]);

  const handleStayLoggedIn = async () => {
    try {
      // Attempt to refresh the token
      const refreshToken = localStorage.getItem("refreshToken");
      if (refreshToken) {
        const { data } = await api.post("/auth/refresh", { refreshToken });
        if (data?.token) {
          api.setToken(data.token);
          if (data.refreshToken) api.setRefreshToken(data.refreshToken);
        }
      }
    } catch {
      // Even if refresh fails, reset the idle timer to extend locally
    }
    resetIdleTimer();
  };

  const handleDismiss = () => {
    setShowWarning(false);
    if (countdownRef.current) clearInterval(countdownRef.current);
  };

  if (!showWarning) return null;

  const mins = Math.floor(remaining / 60);
  const secs = remaining % 60;

  return (
    <div className="session-timeout-banner">
      <div className="session-timeout-content">
        <Clock size={16} />
        <span>
          Your session will expire in <strong>{mins}:{secs.toString().padStart(2, "0")}</strong>
        </span>
        <button className="session-timeout-stay" onClick={handleStayLoggedIn}>
          Stay logged in
        </button>
        <button className="session-timeout-close" onClick={handleDismiss} title="Dismiss">
          <X size={14} />
        </button>
      </div>
    </div>
  );
}

export default SessionTimeoutWarning;
