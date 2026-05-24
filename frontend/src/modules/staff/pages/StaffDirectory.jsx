import { useUserScope } from "../../../core/hooks/useUserScope";
import UserDetailView from "../../../core/components/UserDetailView";
import UserManagement from "../../users/pages/UserManagement";

function StaffDirectory() {
  const { scopedUser, isScoped, clearScope } = useUserScope();

  if (isScoped && scopedUser?.type === "staff") {
    return <UserDetailView userId={scopedUser.id} userType="staff" onBack={clearScope} />;
  }

  return <UserManagement initialTab="staff" hideTabs={true} />;
}

export default StaffDirectory;
