export function toCsv(headers, rows) {
  const esc = (v) => `"${String(v ?? "").replace(/"/g, '""')}"`;
  const lines = [
    headers.join(","),
    ...rows.map((row) => row.map(esc).join(",")),
  ];
  return "\uFEFF" + lines.join("\n");
}

export function downloadCsv(csv, filename) {
  const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename.replace(/[^\w\u0600-\u06FF-]+/g, "_") + ".csv";
  a.click();
  URL.revokeObjectURL(url);
}
