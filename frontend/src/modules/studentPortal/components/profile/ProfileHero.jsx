import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Camera, QrCode, X } from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { AnimatePresence, motion } from "framer-motion";
import { PhotoValidationOverlay } from "../../../../core/components/PhotoValidationOverlay";
import { usePhotoValidator } from "../../../../core/hooks/usePhotoValidator";
import { getLocalized } from "../../../../core/utils/getLocalized";
import PortalBadge from "../shared/PortalBadge";
import { useUploadStudentPhoto } from "../../hooks/useStudentProfile";
import styles from "./ProfileHero.module.css";

const API_ORIGIN = import.meta.env.VITE_API_BASE_URL?.replace("/api", "") || "http://localhost:5256";

function resolvePhotoUrl(photoUrl) {
  if (!photoUrl) return null;
  return photoUrl.startsWith("http") ? photoUrl : `${API_ORIGIN}${photoUrl}`;
}

function ProfileHero({ student, onPhotoUploaded }) {
  const { t, i18n } = useTranslation();
  const fileInputRef = useRef(null);
  const validator = usePhotoValidator();
  const uploadPhoto = useUploadStudentPhoto();

  const [pendingFile, setPendingFile] = useState(null);
  const [previewUrl, setPreviewUrl] = useState(null);
  const [showValidation, setShowValidation] = useState(false);
  const [showQr, setShowQr] = useState(false);

  const displayName = getLocalized(student?.name, i18n.language) || "";
  const photo = resolvePhotoUrl(student?.photoUrl);

  const handleFilePicked = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setPendingFile(file);
    setPreviewUrl(URL.createObjectURL(file));
    setShowValidation(true);
    validator.validate(file);
    e.target.value = "";
  };

  const acceptPhoto = async () => {
    setShowValidation(false);
    try {
      await uploadPhoto.mutateAsync(pendingFile);
      onPhotoUploaded?.();
    } finally {
      setPendingFile(null);
      setPreviewUrl(null);
      validator.reset();
    }
  };

  const rejectPhoto = () => {
    setShowValidation(false);
    setPendingFile(null);
    setPreviewUrl(null);
    validator.reset();
  };

  return (
    <section className={styles.hero}>
      <div className={styles.avatarWrap}>
        {photo ? (
          <img src={photo} alt={displayName} className={styles.avatarImg} loading="lazy" />
        ) : (
          <div className={styles.avatarFallback}>{displayName.charAt(0).toUpperCase() || "S"}</div>
        )}
        <button
          type="button"
          className={styles.cameraBtn}
          onClick={() => fileInputRef.current?.click()}
          aria-label={t("portal_profile.change_photo", { defaultValue: "Change photo" })}
          disabled={uploadPhoto.isPending}
        >
          <Camera size={14} />
        </button>
        <input ref={fileInputRef} type="file" accept="image/*" hidden onChange={handleFilePicked} />
      </div>

      <div className={styles.identity}>
        <h1 className={styles.name}>{displayName}</h1>
        <div className={styles.meta}>
          <PortalBadge tone="primary">{student?.studentCode}</PortalBadge>
          {student?.isActive != null && (
            <PortalBadge tone={student.isActive ? "success" : "danger"}>
              {student.isActive
                ? t("portal_profile.active", { defaultValue: "Active" })
                : t("portal_profile.inactive", { defaultValue: "Inactive" })}
            </PortalBadge>
          )}
        </div>
        <p className={styles.placement}>
          {[student?.facultyName, student?.programName, student?.levelName].filter(Boolean).join(" · ")}
        </p>
      </div>

      <button type="button" className={styles.qrBtn} onClick={() => setShowQr(true)}>
        <QrCode size={18} />
        <span>{t("portal_profile.digital_id", { defaultValue: "Digital ID" })}</span>
      </button>

      <AnimatePresence>
        {showQr && (
          <motion.div
            className={styles.qrOverlay}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={() => setShowQr(false)}
          >
            <motion.div
              className={styles.qrCard}
              initial={{ scale: 0.92, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.92, opacity: 0 }}
              onClick={(e) => e.stopPropagation()}
            >
              <button type="button" className={styles.qrClose} onClick={() => setShowQr(false)} aria-label="Close">
                <X size={16} />
              </button>
              <QRCodeSVG
                value={JSON.stringify({ code: student?.studentCode, id: student?.id })}
                size={180}
                level="M"
              />
              <strong>{displayName}</strong>
              <span>{student?.studentCode}</span>
              <p>{t("portal_profile.digital_id_hint", { defaultValue: "Show this code for on-campus identification" })}</p>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {showValidation && (
        <PhotoValidationOverlay
          results={validator.results}
          previewUrl={previewUrl}
          isProcessing={validator.isProcessing}
          error={validator.error}
          onAccept={acceptPhoto}
          onReject={rejectPhoto}
          onRetry={() => validator.validate(pendingFile)}
        />
      )}
    </section>
  );
}

export default ProfileHero;
