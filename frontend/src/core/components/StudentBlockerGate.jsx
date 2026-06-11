import { useAuth } from "../auth/useAuth";
import { useBlockerState } from "../hooks/useBlockerState";
import ChangePasswordModal from "../auth/components/ChangePasswordModal";
import CompleteProfileWizard from "./CompleteProfileWizard";
import "./studentBlocker.css";

/**
 * Interstitial "blocker" middleware for the student portal. Certain account
 * states must be resolved before a student can reach any standard page; this
 * gate intercepts and renders the mandatory step instead of the route content.
 *
 * Order of precedence (spec Phase 2.2):
 *   1. Password change required (expired password)
 *   2. Profile incomplete (required contact + emergency fields)
 *   3. Pending mandatory action (surveys/acknowledgments — deferred, always false)
 */
function StudentBlockerGate({ children }) {
  const { logout } = useAuth();
  const {
    isLoading,
    requiresPasswordChange,
    profileCompleteness,
    student,
    emergencyData,
    contactData,
  } = useBlockerState();

  if (requiresPasswordChange) {
    // Forced, non-dismissable. Changing the password revokes the session, so the
    // logout below returns the student to login to sign in with the new one.
    return <ChangePasswordModal forced onClose={() => logout()} />;
  }

  if (isLoading) {
    return (
      <div className="blocker-overlay">
        <div className="blocker-loading">
          <div className="spinner" />
          <p>Loading your profile…</p>
        </div>
      </div>
    );
  }

  if (profileCompleteness < 100) {
    return <CompleteProfileWizard student={student} emergencyData={emergencyData} contactData={contactData} />;
  }

  return children;
}

export default StudentBlockerGate;
