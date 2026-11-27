import { Link } from "react-router-dom";

export default function NotFound() {
  return (
    <div className="empty-page">
      <h1 className="display display--small">This page doesn't exist</h1>
      <p className="lede">The link may be mistyped. Profiles live at /u/ followed by a GitHub username.</p>
      <Link to="/" className="btn">Back to home</Link>
    </div>
  );
}
