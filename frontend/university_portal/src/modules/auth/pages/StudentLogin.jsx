import LoginLayout from "../components/LoginLayout";

function StudentLogin() {
  return (
    <LoginLayout
      type="student"
      title="Student Login"
      subtitle="Student Portal Access"
      description="Enter your credentials to access student dashboard"
      redirectPath="/student/profile"
      contactText="Contact admission office"
    />
  );
}

export default StudentLogin;