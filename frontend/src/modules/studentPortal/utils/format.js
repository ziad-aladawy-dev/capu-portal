/**
 * Locale-aware formatting shared by portal pages — date/number output must
 * follow the active i18n language, never the browser default locale.
 * Money amounts stay in Latin digits (fmtAmount in treasuryPaymentService);
 * only the currency label and dates localize.
 */
export function dateLocale(lang) {
  return lang === "ar" ? "ar-EG" : "en-US";
}

/** Date only, e.g. "Jun 12, 2026" / "١٢ يونيو ٢٠٢٦". */
export function formatDate(value, lang, opts) {
  if (!value) return "";
  return new Date(value).toLocaleDateString(dateLocale(lang), opts);
}

/** Date + time, used by timelines and request details. */
export function formatDateTime(value, lang) {
  if (!value) return "";
  return new Date(value).toLocaleString(dateLocale(lang));
}
