import { useEffect } from "react";
import { useNavigate } from "react-router-dom";

export default function AuthCallback() {
  const navigate = useNavigate();

  useEffect(() => {
    // The backend puts the token in the URL fragment (#token=...) so it never
    // reaches a server log. Read it, then replace the URL so it leaves history.
    const params = new URLSearchParams(window.location.hash.slice(1));
    const token = params.get("token");
    if (token) {
      localStorage.setItem("token", token);
      navigate("/dashboard", { replace: true });
    } else {
      navigate("/?error=login_failed", { replace: true });
    }
  }, [navigate]);

  return (
    <div className="page">
      <p>Logging you in...</p>
    </div>
  );
}
