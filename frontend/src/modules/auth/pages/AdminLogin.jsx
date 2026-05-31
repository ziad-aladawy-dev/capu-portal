import LoginLayout from "../components/LoginLayout";

function AdminLogin() {
  return <LoginLayout type="admin" redirectPath="/admin/dashboard" />;
}

export default AdminLogin;
