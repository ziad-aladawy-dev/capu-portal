export function getLocalized(value, lang, fallback = "") {
  if (!value && value !== 0) return fallback;

  if (typeof value === "object") {
    return value[lang] || value.ar || value.en || fallback;
  }

  if (typeof value === "string") {
    try {
      const parsed = JSON.parse(value);
      if (typeof parsed === "object" && parsed !== null) {
        return parsed[lang] || parsed.ar || parsed.en || value;
      }
    } catch {
      // not JSON — plain string, return as-is
    }
    return value;
  }

  return fallback;
}

// Compile a bilingual name pair into the canonical localized JSON string the
// backend stores ({"ar":"…","en":"…"}). A missing language falls back to the
// other so both keys are always populated.
export function toLocalizedJson(ar, en) {
  const arValue = (ar || "").trim();
  const enValue = (en || "").trim();
  return JSON.stringify({ ar: arValue || enValue, en: enValue || arValue });
}

export function parseLocalizedValue(value) {
  if (!value) return { ar: "", en: "" };

  if (typeof value === "object") {
    return {
      ar: value.ar || "",
      en: value.en || "",
    };
  }

  if (typeof value === "string") {
    try {
      const parsed = JSON.parse(value);
      if (typeof parsed === "object" && parsed !== null) {
        return {
          ar: parsed.ar || "",
          en: parsed.en || "",
        };
      }
    } catch {
      // not JSON — treat as plain string for both
    }
    return { ar: value, en: value };
  }

  return { ar: "", en: "" };
}
