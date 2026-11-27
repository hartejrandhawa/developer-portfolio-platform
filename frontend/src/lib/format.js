// Mirrors the backend's HttpUrl rule. Defence in depth: the API already rejects
// non-http(s) URLs, but anything rendered as an href is checked again here.
export function safeUrl(value) {
  if (!value) return null;
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:" ? url.href : null;
  } catch {
    return null;
  }
}

export function parseTechStack(value) {
  if (!value) return [];
  const seen = new Set();
  return value
    .split(",")
    .map((s) => s.trim())
    .filter((s) => {
      const key = s.toLowerCase();
      if (!s || seen.has(key)) return false;
      seen.add(key);
      return true;
    });
}

export function plural(count, word, pluralWord = `${word}s`) {
  return `${count.toLocaleString()} ${count === 1 ? word : pluralWord}`;
}

/** ["a", "b", "c"] -> "a, b and c" */
export function joinWords(parts) {
  if (parts.length <= 1) return parts.join("");
  return `${parts.slice(0, -1).join(", ")} and ${parts.at(-1)}`;
}

export function timeAgo(iso, now = Date.now()) {
  if (!iso) return null;
  // Backend DateTimes are UTC but serialised without a zone suffix.
  const then = new Date(/[zZ]|[+-]\d\d:\d\d$/.test(iso) ? iso : `${iso}Z`).getTime();
  const minutes = Math.round((now - then) / 60000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return `${Math.round(hours / 24)} d ago`;
}

export function shortDate(iso) {
  return new Date(`${iso}T00:00:00Z`).toLocaleDateString(undefined, { month: "short", day: "numeric", timeZone: "UTC" });
}
