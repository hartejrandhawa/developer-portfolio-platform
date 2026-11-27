import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { setToken } from "../lib/auth";

export default function AuthCallback() {
  const navigate = useNavigate();

  useEffect(() => {
    // The backend puts the token in the URL fragment (#token=...) so it never
    // reaches a server log. Read it, then replace the URL so it leaves history.
    const params = new URLSearchParams(window.location.hash.slice(1));
    const token = params.get("token");
    if (token) {
      setToken(token);
      navigate("/dashboard", { replace: true });
    } else {
      navigate("/?error=login_failed", { replace: true });
    }
  }, [navigate]);

  return <p className="muted page-pad" aria-live="polite">Signing you in…</p>;
}
