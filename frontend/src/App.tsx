import { useEffect, useMemo, useRef, useState } from "react";
import ReconnectingWebSocket from "reconnecting-websocket";
import { AdminPage } from "./pages/admin/AdminPage";
import { ClientPage } from "./pages/client/ClientPage";
import { LoadingPage } from "./pages/LoadingPage";
import { ErrorBox } from "./components/ErrorBox";
import { Modal, ModalState } from "./components/Modal";
import { toWsUrl } from "./utils/ws";
import { ApiConfig, AppUser, AuthResponse, CodeHistory, LeaderboardItem } from "./types";

const apiBase = import.meta.env.VITE_API_BASE ?? "/api";
const wsPath = `${(import.meta.env.VITE_API_BASE ?? "/api").replace(/\/$/, "")}/ws/leaderboard`;

const readCsrfFromCookie = () => {
  const match = document.cookie.split(";").map((c) => c.trim()).find((c) => c.startsWith("csrf="));
  return match ? decodeURIComponent(match.split("=")[1]) : "";
};

function displayName(user: { username?: string; first_name?: string; last_name?: string; firstName?: string; lastName?: string }) {
  const u = user.username;
  if (u) return `@${u}`;
  const fn = user.first_name ?? user.firstName ?? "";
  const ln = user.last_name ?? user.lastName ?? "";
  return `${fn} ${ln}`.trim() || "Без имени";
}

function App() {
  const [initData, setInitData] = useState("");
  const [token, setToken] = useState("");
  const [tokenExp, setTokenExp] = useState<Date | null>(null);
  const [user, setUser] = useState<AppUser | null>(null);
  const [config, setConfig] = useState<ApiConfig | null>(null);
  const [history, setHistory] = useState<CodeHistory[]>([]);
  const [leaderboard, setLeaderboard] = useState<LeaderboardItem[]>([]);
  const [redeemCode, setRedeemCode] = useState("");
  const [pointsToGenerate, setPointsToGenerate] = useState(100);
  const [authStarted, setAuthStarted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [authInProgress, setAuthInProgress] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [csrfToken, setCsrfToken] = useState("");
  const [modal, setModal] = useState<ModalState>(null);
  const [wsConnected, setWsConnected] = useState(false);
  const refreshTimer = useRef<number | null>(null);
  const wsRef = useRef<ReconnectingWebSocket | null>(null);

  const logError = (msg: string) => {
    setErrors((prev) => [...prev.slice(-4), msg]);
  };

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const updateTheme = (isDark: boolean) => setTheme(isDark ? "dark" : "light");
    updateTheme(media.matches);
    const listener = (event: MediaQueryListEvent) => updateTheme(event.matches);
    media.addEventListener("change", listener);
    return () => media.removeEventListener("change", listener);
  }, []);

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
  }, [theme]);

  useEffect(() => {
    const csrf = readCsrfFromCookie();
    if (csrf) setCsrfToken(csrf);
  }, []);

  useEffect(() => {
    const tgInitData = window.Telegram?.WebApp?.initData;
    if (tgInitData) {
      setInitData(tgInitData);
    } else {
      logError("initData не найдено. Откройте как Telegram WebApp.");
    }
  }, []);

  useEffect(() => {
    fetch(`${apiBase}/auth/config`)
      .then((res) => res.json())
      .then((cfg) => setConfig(cfg))
      .catch(() => setConfig(null));
  }, []);

  useEffect(() => {
    // Откладываем подключение WS до авторизации
    if (!user) {
      wsRef.current?.close();
      wsRef.current = null;
      setWsConnected(false);
      return;
    }

    const socket = new ReconnectingWebSocket(toWsUrl(wsPath), [], {
      WebSocket: window.WebSocket,
      maxRetries: 20,
      reconnectInterval: 2000
    });
    wsRef.current = socket;

    socket.addEventListener("message", (event) => {
      try {
        const data = JSON.parse(event.data as string) as LeaderboardItem[];
        setLeaderboard(data);
      } catch {
        // ignore malformed messages
      }
    });

    socket.addEventListener("open", () => setWsConnected(true));
    socket.addEventListener("close", () => setWsConnected(false));
    socket.addEventListener("error", () => setWsConnected(false));

    return () => {
      socket.close();
      wsRef.current = null;
    };
  }, [user]);

  useEffect(() => {
    if (!authStarted && initData) {
      setAuthStarted(true);
      void fetchToken();
    }
  }, [authStarted, initData]);

  const parsedExp = useMemo(() => {
    if (!token) return null;
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    try {
      const payload = JSON.parse(atob(parts[1]));
      if (!payload.exp) return null;
      return new Date(payload.exp * 1000);
    } catch {
      return null;
    }
  }, [token]);

  useEffect(() => {
    if (refreshTimer.current) {
      clearTimeout(refreshTimer.current);
      refreshTimer.current = null;
    }
    const exp = parsedExp ?? tokenExp;
    if (!exp || !initData) return;
    const now = Date.now();
    const refreshAt = exp.getTime() - 10_000;
    if (refreshAt <= now) {
      void fetchToken();
      return;
    }
    refreshTimer.current = window.setTimeout(() => void fetchToken(), refreshAt - now);
  }, [parsedExp, tokenExp, initData]);

  const fetchToken = async () => {
    setAuthInProgress(true);
    try {
      const res = await fetch(`${apiBase}/auth/telegram`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ initData })
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || `Request failed (${res.status})`);
      }
      const data: AuthResponse = await res.json();
      setToken(data.token);
      const exp = new Date(data.expiresAt);
      setTokenExp(exp);
      if (data.csrfToken) {
        setCsrfToken(data.csrfToken);
      } else {
        const csrf = readCsrfFromCookie();
        if (csrf) setCsrfToken(csrf);
      }
      await fetchProfile(data.token);
      return data.token;
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Unknown error";
      logError(msg);
      return null;
    } finally {
      setAuthInProgress(false);
    }
  };

  const authFetch = async (path: string, init?: RequestInit, retry = true, tokenOverride?: string): Promise<Response> => {
    const headers = new Headers(init?.headers || {});
    const bearer = tokenOverride ?? token;
    if (bearer) headers.set("Authorization", `Bearer ${bearer}`);
    const method = (init?.method || "GET").toUpperCase();
    if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method) && csrfToken) {
      headers.set("X-CSRF-Token", csrfToken);
    }
    const res = await fetch(`${apiBase}${path}`, { ...init, headers });
    if (res.status === 401 && retry && initData) {
      const newToken = await fetchToken();
      const latestCsrf = readCsrfFromCookie();
      if (latestCsrf) setCsrfToken(latestCsrf);
      if (!newToken) return res;
      const retryHeaders = new Headers(init?.headers || {});
      retryHeaders.set("Authorization", `Bearer ${newToken}`);
      if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method) && (latestCsrf || csrfToken)) {
        retryHeaders.set("X-CSRF-Token", latestCsrf || csrfToken);
      }
      return fetch(`${apiBase}${path}`, { ...init, headers: retryHeaders });
    }
    return res;
  };

  const fetchProfile = async (overrideToken?: string) => {
    const headers = new Headers();
    headers.set("Authorization", `Bearer ${overrideToken ?? token}`);
    const res = await fetch(`${apiBase}/auth/me`, { headers });
    if (!res.ok) return;
    const data = await res.json();
    setUser(data.user);
    if (data.user?.role === "admin") {
      void loadHistory(overrideToken ?? token);
    }
    void loadLeaderboard();
  };

  const loadHistory = async (tokenOverride?: string) => {
    const res = await authFetch("/codes/admin/history", undefined, true, tokenOverride);
    if (!res.ok) return;
    const data = await res.json();
    setHistory(data);
  };

  const loadLeaderboard = async () => {
    const res = await fetch(`${apiBase}/codes/leaderboard`);
    if (!res.ok) return;
    const data = await res.json();
    setLeaderboard(data);
  };

  const redeem = async () => {
    if (!redeemCode.trim()) return;
    setBusy(true);
    try {
      const res = await authFetch("/codes/redeem", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ code: redeemCode.trim() })
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(data.message || `Request failed (${res.status})`);
      }
      setUser((prev) => (prev ? { ...prev, balance: data.balance } : prev));
      setRedeemCode("");
      await loadLeaderboard();
      setModal({
        title: "Успех",
        message: data.message ? `${data.message} Баланс: ${data.balance}` : `Баланс: ${data.balance}`,
        type: "success"
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Unknown error";
      setModal({ title: "Ошибка", message: msg, type: "error" });
    } finally {
      setBusy(false);
    }
  };

  const generateCode = async () => {
    if (pointsToGenerate <= 0) return;
    setBusy(true);
    try {
      const res = await authFetch("/codes/admin/generate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ points: pointsToGenerate })
      });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) {
        throw new Error(data.message || `Request failed (${res.status})`);
      }
      await loadHistory();
      setModal({
        title: "Код создан",
        message: `Код: ${data.code} (+${data.points}), истекает: ${new Date(data.expiresAt).toLocaleString()}`,
        type: "success"
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Unknown error";
      setModal({ title: "Ошибка", message: msg, type: "error" });
    } finally {
      setBusy(false);
    }
  };

  const isLoadingUser = authInProgress || (!user && authStarted && !token && errors.length === 0);

  if (isLoadingUser) {
    return (
      <div className="page">
        <LoadingPage subtitle="Проводится авто-вход через Telegram..." />
        <ErrorBox errors={errors} />
      </div>
    );
  }

  return (
    <div className="page">
      <ErrorBox errors={errors} />
      <Modal modal={modal} onClose={() => setModal(null)} />

      {user && (
        <section className="card">
          <h2>Профиль</h2>
          <div className="user">
            {user.photo_url && <img src={user.photo_url} alt="avatar" width={64} height={64} />}
            <div>
              <div className="user-name">{displayName(user)}</div>
              <div className="muted">Баланс: {user.balance}</div>
              <div className="muted">{user.role === "admin" ? "Админ" : "Клиент"}</div>
              {config?.tokenTtlSeconds ? <div className="muted">TTL токена: {config.tokenTtlSeconds}s</div> : null}
            </div>
          </div>
        </section>
      )}

      {user &&
        (user.role === "admin" ? (
          <AdminPage
            pointsToGenerate={pointsToGenerate}
            onPointsChange={setPointsToGenerate}
            onGenerate={generateCode}
            busy={busy}
            history={history}
            wsConnected={wsConnected}
          />
        ) : (
          <ClientPage
            redeemCode={redeemCode}
            onRedeemCodeChange={setRedeemCode}
            onRedeem={redeem}
            busy={busy}
            leaderboard={leaderboard}
            wsConnected={wsConnected}
          />
        ))}
    </div>
  );
}

export default App;
