import { joinWords, plural } from "./format";

const DAY_MS = 86400000;
export function utc(iso) {
  return new Date(`${iso}T00:00:00Z`);
}

/**
 * Lays the summary out as week columns (Sun..Sat rows), like a calendar, so a
 * given row is always the same weekday. Days outside [from, to] are null.
 */
export function buildGrid({ from, to, days }) {
  const start = utc(from);
  const end = utc(to);
  const counts = new Map(days.map((d) => [d.date, d.count]));
  const first = new Date(start.getTime() - start.getUTCDay() * DAY_MS); // back to Sunday

  const weeks = [];
  for (let t = first.getTime(); t <= end.getTime(); t += 7 * DAY_MS) {
    const week = [];
    for (let i = 0; i < 7; i++) {
      const day = new Date(t + i * DAY_MS);
      const iso = day.toISOString().slice(0, 10);
      week.push(day >= start && day <= end ? { date: iso, count: counts.get(iso) ?? 0 } : null);
    }
    weeks.push(week);
  }

  const max = days.reduce((m, d) => Math.max(m, d.count), 0);
  return { weeks, max };
}

// Relative to the person's own busiest day, so a quiet month still shows shape.
export function level(count, max) {
  if (!count || !max) return 0;
  return Math.min(4, Math.ceil((count / max) * 4));
}

export function activitySentence(activity) {
  const parts = [
    activity.commits > 0 && plural(activity.commits, "commit"),
    activity.pullRequests > 0 && plural(activity.pullRequests, "pull request"),
    activity.issues > 0 && plural(activity.issues, "issue"),
  ].filter(Boolean);
  const repos = activity.topRepos.length;
  return `${joinWords(parts)} across ${repos >= 5 ? "5+ repositories" : plural(repos, "repository", "repositories")} in the last 13 weeks.`;
}
