export function formatDate(iso) {
  if (!iso) return "\u2014";
  try { return new Date(iso).toLocaleDateString(); } catch { return "\u2014"; }
}
