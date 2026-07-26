import { useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

export default function AuthCallback() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();

  useEffect(() => {
    const token = searchParams.get("token");
    if (token) {
      localStorage.setItem("token", token);
      navigate("/dashboard", { replace: true });
    } else {
      navigate("/?error=login_failed", { replace: true });
    }
  }, [searchParams, navigate]);

  return (
    <div className="page">
      <p>Logging you in...</p>
    </div>
  );
}
