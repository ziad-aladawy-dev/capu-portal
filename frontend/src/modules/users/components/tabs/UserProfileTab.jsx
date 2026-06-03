import { User, Mail, Phone, Calendar, Shield, BookOpen, Building2, Award, Hash, Key, CheckCircle, XCircle, Briefcase } from "lucide-react";

function InfoCard({ icon, label, value }) {
  return (
    <div className="detail-card">
      <div className="detail-icon">{icon}</div>
      <div className="detail-content">
        <span className="detail-label">{label}</span>
        <h4 className="detail-value">{value || "Not specified"}</h4>
      </div>
    </div>
  );
}

function UserProfileTab({ user, userType }) {
  const formatDate = (date) => {
    if (!date) return "Not specified";
    return new Date(date).toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" });
  };

  const formatDateTime = (date) => {
    if (!date) return "Never";
    return new Date(date).toLocaleString("en-US", {
      year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit",
    });
  };

  const isPasswordExpired = user?.passwordStatus === "Expired";

  return (
    <div>
      <div className="details-grid">
        <InfoCard icon={<Hash size={19} />} label="National ID" value={user.nationalId} />
        <InfoCard icon={<User size={19} />} label="Full Name" value={user.name} />
        <InfoCard icon={<Calendar size={19} />} label="Date of Birth" value={formatDate(user.birthDate)} />
        <InfoCard icon={<Phone size={19} />} label="Phone" value={user.phoneNumber} />
        <InfoCard icon={<Mail size={19} />} label="Email" value={user.email} />
      </div>

      <h3 className="section-title" style={{ marginTop: 20 }}>
        {userType === "student" ? "Academic Information" : "Employment Information"}
      </h3>
      <div className="details-grid">
        {userType === "student" ? (
          <>
            <InfoCard icon={<Award size={19} />} label="Student Code" value={user.studentCode} />
            <InfoCard icon={<Building2 size={19} />} label="Faculty" value={user.facultyName} />
            <InfoCard icon={<BookOpen size={19} />} label="Program" value={user.programName} />
            <InfoCard icon={<Award size={19} />} label="Level" value={user.levelName} />
            <InfoCard icon={<Shield size={19} />} label="Academic Status" value={user.status || "Active"} />
          </>
        ) : (
          <>
            <InfoCard icon={<Award size={19} />} label="Employee Code" value={user.employeeCode} />
            <InfoCard icon={<Shield size={19} />} label="Role" value={user.role} />
            <InfoCard icon={<Briefcase size={19} />} label="Job Title" value={user.jobTitle} />
            <InfoCard icon={<Building2 size={19} />} label="Faculty / Department" value={user.facultyName || user.structureNodeName} />
          </>
        )}
      </div>

      <h3 className="section-title" style={{ marginTop: 20 }}>Account Details</h3>
      <div className="details-grid">
        <InfoCard icon={<Calendar size={19} />} label="Account Created" value={formatDateTime(user.createdAt)} />
        <InfoCard icon={<Calendar size={19} />} label="Last Updated" value={formatDateTime(user.updatedAt)} />
        <InfoCard icon={<Key size={19} />} label="Password Status" value={user.passwordStatus || (isPasswordExpired ? "Expired" : "Valid")} />
        <InfoCard icon={user.isActive ? <CheckCircle size={19} /> : <XCircle size={19} />} label="Account Status" value={user.isActive ? "Active" : "Inactive"} />
      </div>
    </div>
  );
}

export default UserProfileTab;
