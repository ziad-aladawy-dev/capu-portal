import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  User, Phone, ShieldAlert, Users, GraduationCap, KeyRound, ShieldCheck,
} from "lucide-react";
import { useToast } from "../../../core/components/Toast";
import { parseLocalizedValue } from "../../../core/utils/getLocalized";
import { buildCompleteness } from "../../../core/hooks/useBlockerState";
import { STUDENT_PROFILE_CATEGORY } from "../../../core/services/studentProfileService";
import ChangePasswordModal from "../../../core/auth/components/ChangePasswordModal";
import PortalPageShell from "../components/shared/PortalPageShell";
import PortalSkeleton from "../components/shared/PortalSkeleton";
import PortalEmptyState from "../components/shared/PortalEmptyState";
import PortalBadge from "../components/shared/PortalBadge";
import ProfileHero from "../components/profile/ProfileHero";
import ProfileSection from "../components/profile/ProfileSection";
import ProfileCompleteness from "../components/profile/ProfileCompleteness";
import {
  useStudentProfile, useUpdateStudent, useUpsertProfileRecord, CONTACT_RECORD_KEY,
} from "../hooks/useStudentProfile";
import styles from "./StudentProfile.module.css";

const PHONE_RE = /^01[0-9]{9}$/;
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function StudentProfile() {
  const { t } = useTranslation();
  const { addToast } = useToast();
  const profile = useStudentProfile();
  const updateStudent = useUpdateStudent();
  const upsertRecord = useUpsertProfileRecord();

  const [highlightSection, setHighlightSection] = useState(null);
  const [showChangePassword, setShowChangePassword] = useState(false);

  if (profile.isLoading) {
    return (
      <PortalPageShell title={t("portal_profile.title", { defaultValue: "My Profile" })}>
        <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
          <PortalSkeleton variant="block" height={120} />
          <PortalSkeleton variant="block" height={64} />
          <PortalSkeleton variant="block" height={180} />
          <PortalSkeleton variant="block" height={180} />
        </div>
      </PortalPageShell>
    );
  }

  if (profile.isError || !profile.data?.student) {
    return (
      <PortalPageShell title={t("portal_profile.title", { defaultValue: "My Profile" })}>
        <PortalEmptyState
          icon={User}
          title={t("portal_profile.load_failed", { defaultValue: "Couldn't load your profile" })}
          onAction={() => profile.refetch()}
          actionLabel={t("retry", { defaultValue: "Retry" })}
        />
      </PortalPageShell>
    );
  }

  const { student, records, emergency, contact } = profile.data;
  const { ar: nameAr, en: nameEn } = parseLocalizedValue(student.name);
  const completeness = buildCompleteness(student, records);

  const toastSaved = () =>
    addToast(t("portal_profile.saved", { defaultValue: "Saved" }), "success");
  const toastFailed = (err) =>
    addToast(err?.response?.data?.message || t("portal_profile.save_failed", { defaultValue: "Save failed" }), "error");

  const jumpToSection = (section) => {
    setHighlightSection(section);
    document.querySelector(`[data-section="${section}"]`)?.scrollIntoView({ behavior: "smooth", block: "center" });
    setTimeout(() => setHighlightSection(null), 1200);
  };

  const validatePhone = (v) =>
    v && !PHONE_RE.test(v) ? t("portal_profile.invalid_phone", { defaultValue: "Use an Egyptian mobile number (01XXXXXXXXX)" }) : null;
  const validateEmail = (v) =>
    v && !EMAIL_RE.test(v) ? t("portal_profile.invalid_email", { defaultValue: "Invalid email format" }) : null;

  const saveContact = async (values) => {
    try {
      const entityPatch = {};
      if (values.email !== student.email) entityPatch.email = values.email.trim();
      if (values.phoneNumber !== student.phoneNumber) entityPatch.phoneNumber = values.phoneNumber.trim();
      if (Object.keys(entityPatch).length > 0) await updateStudent.mutateAsync(entityPatch);

      await upsertRecord.mutateAsync({
        category: STUDENT_PROFILE_CATEGORY.Custom,
        customKey: CONTACT_RECORD_KEY,
        data: {
          address: values.address?.trim() ?? "",
          city: values.city?.trim() ?? "",
          country: values.country?.trim() ?? "",
        },
      });
      toastSaved();
    } catch (err) {
      toastFailed(err);
      throw err;
    }
  };

  const saveEmergency = async (values) => {
    try {
      await upsertRecord.mutateAsync({
        category: STUDENT_PROFILE_CATEGORY.EmergencyContact,
        data: {
          name: values.name?.trim() ?? "",
          relationship: values.relationship?.trim() ?? "",
          phone: values.phone?.trim() ?? "",
        },
      });
      toastSaved();
    } catch (err) {
      toastFailed(err);
      throw err;
    }
  };

  const saveGuardian = async (values) => {
    try {
      await updateStudent.mutateAsync({
        guardianName: values.guardianName?.trim() ?? "",
        guardianPhone: values.guardianPhone?.trim() ?? "",
      });
      toastSaved();
    } catch (err) {
      toastFailed(err);
      throw err;
    }
  };

  const verifiedBadge = (
    <PortalBadge tone="info" icon={ShieldCheck}>
      {t("portal_profile.verified", { defaultValue: "Verified" })}
    </PortalBadge>
  );

  return (
    <PortalPageShell
      title={t("portal_profile.title", { defaultValue: "My Profile" })}
      subtitle={t("portal_profile.subtitle", { defaultValue: "Your personal information and account settings" })}
    >
      <ProfileHero student={student} onPhotoUploaded={toastSaved} />

      <ProfileCompleteness
        percentage={completeness.overallPercentage}
        missingFields={completeness.missingFields}
        onJump={jumpToSection}
      />

      <div className={styles.sections}>
        <ProfileSection
          id="personal"
          icon={User}
          title={t("portal_profile.personal", { defaultValue: "Personal Information" })}
          badge={verifiedBadge}
          fields={[
            { key: "nameAr", label: t("portal_profile.name_ar", { defaultValue: "Name (Arabic)" }), value: nameAr },
            { key: "nameEn", label: t("portal_profile.name_en", { defaultValue: "Name (English)" }), value: nameEn },
            { key: "birthDate", label: t("portal_profile.dob", { defaultValue: "Date of Birth" }), value: student.birthDate ? new Date(student.birthDate).toLocaleDateString() : "" },
            { key: "nationalId", label: t("portal_profile.national_id", { defaultValue: "National ID" }), value: student.nationalId },
            { key: "gender", label: t("portal_profile.gender", { defaultValue: "Gender" }), value: student.gender },
          ]}
        />

        <ProfileSection
          id="contact"
          icon={Phone}
          title={t("portal_profile.contact", { defaultValue: "Contact Information" })}
          highlight={highlightSection === "contact"}
          onSave={saveContact}
          fields={[
            { key: "email", label: t("portal_profile.email", { defaultValue: "Email" }), value: student.email, editable: true, required: true, type: "email", validate: validateEmail },
            { key: "phoneNumber", label: t("portal_profile.phone", { defaultValue: "Phone" }), value: student.phoneNumber, editable: true, required: true, type: "tel", validate: validatePhone },
            { key: "address", label: t("portal_profile.address", { defaultValue: "Address" }), value: contact.address, editable: true, required: true },
            { key: "city", label: t("portal_profile.city", { defaultValue: "City" }), value: contact.city, editable: true },
            { key: "country", label: t("portal_profile.country", { defaultValue: "Country" }), value: contact.country, editable: true },
          ]}
        />

        <ProfileSection
          id="emergency"
          icon={ShieldAlert}
          title={t("portal_profile.emergency", { defaultValue: "Emergency Contact" })}
          highlight={highlightSection === "emergency"}
          onSave={saveEmergency}
          fields={[
            { key: "name", label: t("portal_profile.contact_name", { defaultValue: "Name" }), value: emergency.name, editable: true, required: true },
            { key: "relationship", label: t("portal_profile.relationship", { defaultValue: "Relationship" }), value: emergency.relationship, editable: true },
            { key: "phone", label: t("portal_profile.phone", { defaultValue: "Phone" }), value: emergency.phone, editable: true, required: true, type: "tel", validate: validatePhone },
          ]}
        />

        <ProfileSection
          id="guardian"
          icon={Users}
          title={t("portal_profile.guardian", { defaultValue: "Guardian Information" })}
          onSave={saveGuardian}
          fields={[
            { key: "guardianName", label: t("portal_profile.guardian_name", { defaultValue: "Guardian Name" }), value: student.guardianName, editable: true },
            { key: "guardianPhone", label: t("portal_profile.guardian_phone", { defaultValue: "Guardian Phone" }), value: student.guardianPhone, editable: true, type: "tel", validate: validatePhone },
          ]}
        />

        <ProfileSection
          id="academic"
          icon={GraduationCap}
          title={t("portal_profile.academic", { defaultValue: "Academic Placement" })}
          badge={verifiedBadge}
          fields={[
            { key: "studentCode", label: t("portal_profile.student_code", { defaultValue: "Student Code" }), value: student.studentCode },
            { key: "faculty", label: t("portal_profile.faculty", { defaultValue: "Faculty" }), value: student.facultyName },
            { key: "program", label: t("portal_profile.program", { defaultValue: "Program" }), value: student.programName },
            { key: "level", label: t("portal_profile.level", { defaultValue: "Level" }), value: student.levelName },
            { key: "memberSince", label: t("portal_profile.member_since", { defaultValue: "Member Since" }), value: student.createdAt ? new Date(student.createdAt).toLocaleDateString() : "" },
          ]}
        />

        <ProfileSection
          id="security"
          icon={KeyRound}
          title={t("portal_profile.security", { defaultValue: "Security" })}
          defaultOpen={false}
          fields={[
            {
              key: "password",
              label: t("portal_profile.password", { defaultValue: "Password" }),
              value: "********",
              render: () => (
                <button type="button" className={styles.passwordBtn} onClick={() => setShowChangePassword(true)}>
                  <KeyRound size={13} /> {t("portal_profile.change_password", { defaultValue: "Change password" })}
                </button>
              ),
            },
            {
              key: "passwordExpiry",
              label: t("portal_profile.password_expiry", { defaultValue: "Password Expires" }),
              value: student.passwordExpiry ? new Date(student.passwordExpiry).toLocaleDateString() : "",
            },
          ]}
        />
      </div>

      {showChangePassword && <ChangePasswordModal onClose={() => setShowChangePassword(false)} />}
    </PortalPageShell>
  );
}

export default StudentProfile;
