import LoginLayout from "../components/LoginLayout";

function AdminLogin() {
  return (
    <LoginLayout
      type="admin"
      title="Admin Login"
      subtitle="Staff / Administrator Access"
      description="Enter your credentials to access the dashboard"
      redirectPath="/dashboard"
      contactText="Contact system admin"
    />
  );
}

export default AdminLogin;