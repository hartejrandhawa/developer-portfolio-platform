import { parseTechStack } from "../lib/format";

export default function TechChips({ value }) {
  const items = parseTechStack(value);
  if (items.length === 0) return null;
  return (
    <ul className="chips" aria-label="Built with">
      {items.map((item) => (
        <li key={item} className="chip">{item}</li>
      ))}
    </ul>
  );
}
