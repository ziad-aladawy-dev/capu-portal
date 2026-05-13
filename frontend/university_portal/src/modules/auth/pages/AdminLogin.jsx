import LoginLayout from "../components/LoginLayout";

function AdminLogin() {
  return (
    <LoginLayout
      type="admin"
      title="Admin / Staff Portal"
      subtitle="Enter your credentials to continue"
      redirectPath="/admin/dashboard"
      contactText="Contact system admin"
    />
  );
}

export default AdminLogin;
