function ErrorMessage({ message = "Something went wrong" }) {
  return (
    <div
      style={{
        padding: "14px 16px",
        borderRadius: 12,
        background: "rgba(220,38,38,0.08)",
        border: "1px solid rgba(220,38,38,0.18)",
        color: "#dc2626",
        fontWeight: 700,
      }}
    >
      {message}
    </div>
  );
}
export default ErrorMessage;
