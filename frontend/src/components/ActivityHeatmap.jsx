import { buildGrid, level, utc } from "../lib/activity";
import { plural, shortDate } from "../lib/format";

const WEEKDAY_LABELS = ["", "Mon", "", "Wed", "", "Fri", ""];

function monthLabel(week, index) {
  const firstDay = week.find(Boolean);
  if (!firstDay) return "";
  const date = utc(firstDay.date);
  // Label the week a month starts in. The first column gets its month only if
  // that month runs on for a while, otherwise it collides with the next label.
  if (index === 0 ? date.getUTCDate() > 21 : date.getUTCDate() > 7) return "";
  return date.toLocaleDateString(undefined, { month: "short", timeZone: "UTC" });
}

export default function ActivityHeatmap({ activity, size = "large", animate = false }) {
  const { weeks, max } = buildGrid(activity);
  const label = `${plural(activity.total, "contribution")} between ${shortDate(activity.from)} and ${shortDate(activity.to)}`;

  return (
    <figure
      className={`heatmap heatmap--${size}${animate ? " heatmap--animate" : ""}`}
      style={{ "--weeks": weeks.length }}
    >
      <div className="heatmap-grid" role="img" aria-label={label}>
        <span aria-hidden="true" />
        {weeks.map((week, w) => (
          <span key={`m${w}`} className="heatmap-month" aria-hidden="true">
            {monthLabel(week, w)}
          </span>
        ))}
        {WEEKDAY_LABELS.map((dayLabel, row) => (
          <Row key={row} row={row} label={dayLabel} weeks={weeks} max={max} />
        ))}
      </div>
    </figure>
  );
}

function Row({ row, label, weeks, max }) {
  return (
    <>
      <span className="heatmap-weekday" aria-hidden="true">{label}</span>
      {weeks.map((week, w) => {
        const cell = week[row];
        if (!cell) return <span key={w} className="heatmap-cell heatmap-cell--empty" />;
        return (
          <span
            key={w}
            className="heatmap-cell"
            data-level={level(cell.count, max)}
            style={{ "--i": w * 7 + row }}
            title={`${plural(cell.count, "contribution")} on ${shortDate(cell.date)}`}
          />
        );
      })}
    </>
  );
}
