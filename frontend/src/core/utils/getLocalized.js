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
