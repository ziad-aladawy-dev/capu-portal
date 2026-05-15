import UserManagement from "../../users/pages/UserManagement";

function StudentDirectory() {
  return <UserManagement initialTab="students" hideTabs={true} />;
}

export default StudentDirectory;
