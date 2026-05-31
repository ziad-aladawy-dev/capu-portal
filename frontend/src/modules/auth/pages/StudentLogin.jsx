import LoginLayout from "../components/LoginLayout";

function StudentLogin() {
  return <LoginLayout type="student" redirectPath="/student/dashboard" />;
}

export default StudentLogin;
