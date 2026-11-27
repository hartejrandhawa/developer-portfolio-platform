import { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { getToken, setToken } from "../lib/auth";

export default function AuthCallback() {
  const navigate = useNavigate();

  useEffect(() => {
    // The backend puts the token in the URL fragment (#token=...) so it never
    // reaches a server log.
    const token = new URLSearchParams(window.location.hash.slice(1)).get(
      "token",
    );
    if (token) setToken(token);

    // Decide by whether we now hold a token, not by whether the fragment still
    // has one: navigating away rewrites the URL immediately, so a second run of
    // this effect (React StrictMode does this in dev) would otherwise see an
    // empty fragment and wrongly report a failed login.
    navigate(getToken() ? "/dashboard" : "/?error=login_failed", {
      replace: true,
    });
  }, [navigate]);

  return (
    <p className="muted page-pad" aria-live="polite">
      Signing you in…
    </p>
  );
}
