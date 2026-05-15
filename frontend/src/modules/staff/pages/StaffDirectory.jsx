import UserManagement from "../../users/pages/UserManagement";

function StaffDirectory() {
  return <UserManagement initialTab="staff" hideTabs={true} />;
}

export default StaffDirectory;
