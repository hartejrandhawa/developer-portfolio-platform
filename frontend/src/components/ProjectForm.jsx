import { useId, useState } from "react";
import TechChips from "./TechChips";

const EMPTY = { title: "", description: "", repoUrl: "", liveUrl: "", techStack: "" };
// Same rule the API enforces; the browser's own type="url" check would accept javascript:.
const HTTP_PATTERN = "https?://.+";

export default function ProjectForm({ initial, submitLabel, onSubmit, onCancel }) {
  const [form, setForm] = useState(() => ({ ...EMPTY, ...stripNulls(initial) }));
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(false);
  const id = useId();

  const set = (field) => (e) => setForm((f) => ({ ...f, [field]: e.target.value }));

  async function handleSubmit(e) {
    e.preventDefault();
    setSaving(true);
    setError(null);
    try {
      await onSubmit(form);
    } catch (err) {
      setError(err.message);
      setSaving(false);
    }
  }

  return (
    <form className="project-form" onSubmit={handleSubmit}>
      <div className="field">
        <label htmlFor={`${id}-title`}>Title</label>
        <input id={`${id}-title`} value={form.title} onChange={set("title")} maxLength={100} required autoFocus />
      </div>
      <div className="field">
        <label htmlFor={`${id}-desc`}>What it does</label>
        <textarea id={`${id}-desc`} value={form.description} onChange={set("description")} maxLength={1000} rows={3} />
      </div>
      <div className="field-row">
        <div className="field">
          <label htmlFor={`${id}-repo`}>Code link</label>
          <input id={`${id}-repo`} type="url" inputMode="url" pattern={HTTP_PATTERN} placeholder="https://github.com/…"
            value={form.repoUrl} onChange={set("repoUrl")} maxLength={2048} />
        </div>
        <div className="field">
          <label htmlFor={`${id}-live`}>Live site</label>
          <input id={`${id}-live`} type="url" inputMode="url" pattern={HTTP_PATTERN} placeholder="https://…"
            value={form.liveUrl} onChange={set("liveUrl")} maxLength={2048} />
        </div>
      </div>
      <div className="field">
        <label htmlFor={`${id}-tech`}>Built with</label>
        <input id={`${id}-tech`} value={form.techStack} onChange={set("techStack")} maxLength={300}
          placeholder="React, ASP.NET Core, SQL Server" aria-describedby={`${id}-tech-hint`} />
        <p id={`${id}-tech-hint`} className="hint">Separate with commas.</p>
        <TechChips value={form.techStack} />
      </div>
      {error && <p className="form-error" role="alert">{error}</p>}
      <div className="actions">
        <button type="submit" className="btn btn--primary" disabled={saving}>
          {saving ? "Saving…" : submitLabel}
        </button>
        <button type="button" className="btn btn--quiet" onClick={onCancel}>Cancel</button>
      </div>
    </form>
  );
}

function stripNulls(obj = {}) {
  return Object.fromEntries(Object.entries(obj).map(([k, v]) => [k, v ?? ""]));
}
