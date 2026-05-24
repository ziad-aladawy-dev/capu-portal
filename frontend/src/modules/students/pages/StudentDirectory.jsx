import { useUserScope } from "../../../core/hooks/useUserScope";
import UserDetailView from "../../../core/components/UserDetailView";
import UserManagement from "../../users/pages/UserManagement";

function StudentDirectory() {
  const { scopedUser, isScoped, clearScope } = useUserScope();

  if (isScoped && scopedUser?.type === "student") {
    return <UserDetailView userId={scopedUser.id} userType="student" onBack={clearScope} />;
  }

  return <UserManagement initialTab="students" hideTabs={true} />;
}

export default StudentDirectory;
