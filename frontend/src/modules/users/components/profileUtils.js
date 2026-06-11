/* Formatting helpers shared by the profile detail pages. */

export const fmtDate = (d) =>
  d ? new Date(d).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" }) : "—";

export const fmtDateTime = (d) =>
  d ? new Date(d).toLocaleString("en-US", { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : "—";

export const fmtMoney = (n, currency = "EGP") =>
  `${Number(n || 0).toLocaleString("en-US", { minimumFractionDigits: 2 })} ${currency}`;

export function yearsSince(date) {
  if (!date) return null;
  const diff = Date.now() - new Date(date).getTime();
  return Math.floor(diff / (365.25 * 24 * 3600 * 1000));
}

/** Signed whole days from now until `date` (negative = past). */
export function daysUntil(date) {
  if (!date) return null;
  return Math.ceil((new Date(date).getTime() - Date.now()) / (24 * 3600 * 1000));
}

const API_ORIGIN = import.meta.env.VITE_API_BASE_URL?.replace("/api", "") || "http://localhost:5256";

export function resolvePhotoUrl(photoUrl) {
  if (!photoUrl) return null;
  return photoUrl.startsWith("http") ? photoUrl : `${API_ORIGIN}${photoUrl}`;
}
