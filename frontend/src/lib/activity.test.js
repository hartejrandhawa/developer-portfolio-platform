import { describe, expect, it } from "vitest";
import { activitySentence, buildGrid, level } from "./activity";

describe("buildGrid", () => {
  // 2026-11-04 is a Wednesday.
  const grid = buildGrid({
    from: "2026-11-04",
    to: "2026-11-17",
    days: [{ date: "2026-11-05", count: 3 }, { date: "2026-11-17", count: 6 }],
  });

  it("aligns columns to weeks starting on Sunday", () => {
    expect(grid.weeks).toHaveLength(3);
    expect(grid.weeks[0].slice(0, 3)).toEqual([null, null, null]); // Sun-Tue before range
    expect(grid.weeks[0][3]).toEqual({ date: "2026-11-04", count: 0 });
    expect(grid.weeks[0][4]).toEqual({ date: "2026-11-05", count: 3 });
  });

  it("leaves days after the range empty and reports the max", () => {
    expect(grid.weeks[2][2]).toEqual({ date: "2026-11-17", count: 6 });
    expect(grid.weeks[2][3]).toBeNull();
    expect(grid.max).toBe(6);
  });
});

describe("level", () => {
  it("scales relative to the busiest day", () => {
    expect(level(0, 10)).toBe(0);
    expect(level(1, 10)).toBe(1);
    expect(level(5, 10)).toBe(2);
    expect(level(10, 10)).toBe(4);
    expect(level(3, 0)).toBe(0);
  });
});

describe("activitySentence", () => {
  it("only mentions the kinds of activity that happened", () => {
    const sentence = activitySentence({ commits: 12, pullRequests: 0, issues: 1, topRepos: [{}, {}] });
    expect(sentence).toBe("12 commits and 1 issue across 2 repositories in the last 13 weeks.");
  });

  it("caps the repository count at the top-5 list size", () => {
    const sentence = activitySentence({ commits: 1, pullRequests: 0, issues: 0, topRepos: [{}, {}, {}, {}, {}] });
    expect(sentence).toBe("1 commit across 5+ repositories in the last 13 weeks.");
  });
});
