import { describe, expect, it } from "vitest";
import { joinWords, parseTechStack, plural, safeUrl, timeAgo } from "./format";

describe("safeUrl", () => {
  it.each([
    ["https://example.com/a?b=1", "https://example.com/a?b=1"],
    ["http://localhost:3000", "http://localhost:3000/"],
  ])("keeps http(s) URL %s", (input, expected) => {
    expect(safeUrl(input)).toBe(expected);
  });

  it.each(["javascript:alert(1)", " JavaScript:alert(1)", "data:text/html,hi", "/relative", "nope", "", null])(
    "rejects %s",
    (input) => {
      expect(safeUrl(input)).toBeNull();
    },
  );
});

describe("parseTechStack", () => {
  it("trims, drops blanks and dedupes case-insensitively", () => {
    expect(parseTechStack(" React, ,Node.js,react ,  Go ")).toEqual(["React", "Node.js", "Go"]);
  });

  it("handles empty values", () => {
    expect(parseTechStack(null)).toEqual([]);
    expect(parseTechStack("")).toEqual([]);
  });
});

describe("text helpers", () => {
  it("pluralises", () => {
    expect(plural(1, "commit")).toBe("1 commit");
    expect(plural(2, "repository", "repositories")).toBe("2 repositories");
  });

  it("joins lists for sentences", () => {
    expect(joinWords(["a"])).toBe("a");
    expect(joinWords(["a", "b"])).toBe("a and b");
    expect(joinWords(["a", "b", "c"])).toBe("a, b and c");
  });

  it("treats zone-less timestamps from the API as UTC", () => {
    const now = Date.parse("2026-11-27T12:00:00Z");
    expect(timeAgo("2026-11-27T11:55:00", now)).toBe("5 min ago");
    expect(timeAgo("2026-11-27T09:00:00Z", now)).toBe("3 h ago");
    expect(timeAgo(null, now)).toBeNull();
  });
});
