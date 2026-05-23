import LoginLayout from "../components/LoginLayout";

function StudentLogin() {
  return (
    <LoginLayout
      type="student"
      title="Student Portal"
      subtitle="Enter your credentials to continue"
      redirectPath="/student/dashboard"
      contactText="Contact admission office"
    />
  );
}

export default StudentLogin;
