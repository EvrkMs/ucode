import { lazy, Suspense, useEffect, useMemo, useRef, useState } from "react";
import { ErrorBox } from "./components/ErrorBox";
import { Modal, ModalState } from "./components/Modal";
import { LoadingPage } from "./pages/LoadingPage";
import { toWsUrl } from "./utils/ws";
import { dataCache } from "./utils/cache";
import { ApiConfig, AppUser, AuthResponse, CodeHistory, LeaderboardItem, RootUser } from "./types";
import { sendDiag } from "./utils/diag";

// Lazy loading компонентов для уменьшения initial bundle
const AdminPage = lazy(() => import("./pages/admin/AdminPage").then(m => ({ default: m.AdminPage })));
const ClientPage = lazy(() => import("./pages/client/ClientPage").then(m => ({ default: m.ClientPage })));

// Динамический импорт WebSocket только когда он нужен
let ReconnectingWebSocket: typeof import("reconnecting-websocket").default | null = null;
const loadWebSocket = async () => {
  if (!ReconnectingWebSocket) {
    const module = await import("reconnecting-websocket");
    ReconnectingWebSocket = module.default;
  }
  return ReconnectingWebSocket;
};

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
  const [pointsToGenerate, setPointsToGenerate] = useState(1);
  const [authStarted, setAuthStarted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [authInProgress, setAuthInProgress] = useState(false);
  const [errors, setErrors] = useState<string[]>([]);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const [csrfToken, setCsrfToken] = useState("");
  const [modal, setModal] = useState<ModalState>(null);
  const [wsConnected, setWsConnected] = useState(false);
  const [loading, setLoading] = useState({ profile: false, history: false, leaderboard: false });
  const refreshTimer = useRef<number | null>(null);
  const wsRef = useRef<any>(null);
  const sdkMissingLogged = useRef(false);
  const lastAuthStart = useRef<number | null>(null);
  const [rootSearchQuery, setRootSearchQuery] = useState("");
  const [rootResults, setRootResults] = useState<RootUser[]>([]);
  const [rootBusy, setRootBusy] = useState(false);
  const [rootError, setRootError] = useState<string | null>(null);

  const logError = (msg: string) => {
    setErrors((prev) => [...prev.slice(-4), msg]);
  };

  const updateLoading = (key: keyof typeof loading, value: boolean) => {
    setLoading((prev) => ({ ...prev, [key]: value }));
  };

  const fetchWithTimeout = async (url: string, options: RequestInit = {}, timeout = 15000) => {
    if (typeof AbortController === "undefined") {
      return fetch(url, options);
    }
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      try {
        controller.abort();
      } catch (err) {
        console.warn("Abort failed", err);
      }
    }, timeout);
    try {
      return await fetch(url, { ...options, signal: controller.signal });
    } catch (err) {
      if (err instanceof Error && err.name === "AbortError") {
        console.warn("Request timed out or aborted", url);
      }
      throw err;
    } finally {
      clearTimeout(timer);
    }
  };

  const fetchWithRetry = async <T,>(fn: () => Promise<T>, maxRetries = 3, delayMs = 800) => {
    let lastError: unknown;
    for (let attempt = 0; attempt < maxRetries; attempt++) {
      try {
        return await fn();
      } catch (err) {
        lastError = err;
        if (attempt === maxRetries - 1) break;
        await new Promise((resolve) => setTimeout(resolve, delayMs * (attempt + 1)));
      }
    }
    throw lastError ?? new Error("Unknown error");
  };

  useEffect(() => {
    document.documentElement.setAttribute("data-theme", theme);
  }, [theme]);

  useEffect(() => {
    const csrf = readCsrfFromCookie();
    if (csrf) setCsrfToken(csrf);
  }, []);

  useEffect(() => {
    let mounted = true;
    const initTelegram = () => {
      const tg = window.Telegram?.WebApp;
      if (!tg) {
        if (mounted) window.setTimeout(initTelegram, 100);
        return;
      }
      try {
        tg.ready();
        tg.expand();
        const tgInitData = tg.initData;
        if (tgInitData) {
          setInitData(tgInitData);
          sendDiag("tg-init-ok");
        } else {
          logError("initData не найдено. Откройте как Telegram WebApp.");
          sendDiag("tg-initdata-missing");
        }
      } catch (err) {
        console.error("Telegram init failed", err);
        logError("Ошибка инициализации Telegram WebApp");
        sendDiag("tg-init-failed", err instanceof Error ? err.message : "unknown");
      }
    };
    initTelegram();
    return () => {
      mounted = false;
    };
  }, []);

  useEffect(() => {
    const tg = window.Telegram?.WebApp;
    const setViewportVar = (height: number) => {
      document.documentElement.style.setProperty("--tg-viewport-height", `${height}px`);
    };

    const applyThemeFromTelegram = () => {
      const scheme = tg?.colorScheme === "dark" ? "dark" : "light";
      if (scheme) setTheme(scheme);
      const params = tg?.themeParams;
      const root = document.documentElement.style;
      if (params?.bg_color) root.setProperty("--bg", params.bg_color);
      if (params?.secondary_bg_color) root.setProperty("--card", params.secondary_bg_color);
      if (params?.text_color) root.setProperty("--text", params.text_color);
      if (params?.hint_color) root.setProperty("--muted", params.hint_color);
      if (params?.button_color) root.setProperty("--accent", params.button_color);
      if (params?.link_color) root.setProperty("--link", params.link_color);
      if (params?.button_text_color) root.setProperty("--buttonText", params.button_text_color);
    };

    const updateViewport = () => {
        if (tg?.viewportHeight) {
          setViewportVar(tg.stableViewportHeight || tg.viewportHeight);
        } else {
          setViewportVar(window.innerHeight);
        }
    };

    if (tg) {
      tg.ready();
      tg.expand();
      applyThemeFromTelegram();
      updateViewport();
      tg.setBackgroundColor?.(tg.themeParams?.bg_color ?? "#ffffff");
      const headerColor = tg.themeParams?.secondary_bg_color ?? tg.themeParams?.bg_color;
      if (headerColor) tg.setHeaderColor?.(headerColor);
      tg.onEvent("themeChanged", applyThemeFromTelegram);
      tg.onEvent("viewportChanged", updateViewport);
      return () => {
        tg.offEvent("themeChanged", applyThemeFromTelegram);
        tg.offEvent("viewportChanged", updateViewport);
      };
    }

    applyThemeFromTelegram();
    updateViewport();
  }, []);

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const updateTheme = (isDark: boolean) => setTheme(isDark ? "dark" : "light");
    updateTheme(media.matches);
    const listener = (event: MediaQueryListEvent) => updateTheme(event.matches);
    media.addEventListener("change", listener);
    return () => media.removeEventListener("change", listener);
  }, []);

  useEffect(() => {
    const handleOnline = () => {
      if (user) {
        void fetchProfile();
      }
    };
    const handleOffline = () =>
      setModal({
        title: "Нет соединения",
        message: "Проверьте подключение к интернету",
        type: "error"
      });
    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [user]);

  useEffect(() => {
    fetchWithTimeout(`${apiBase}/auth/config`)
      .then((res) => res.json())
      .then((cfg) => setConfig(cfg))
      .catch(() => setConfig(null));
  }, []);

  useEffect(() => {
    // Откладываем подключение WS до авторизации и загрузку библиотеки
    if (!user || !token) {
      wsRef.current?.close();
      wsRef.current = null;
      setWsConnected(false);
      return;
    }

    let mounted = true;
    const connectTimer = window.setTimeout(async () => {
      if (!mounted) return;
      try {
        // Динамически загружаем WebSocket только когда авторизованы
        const WS = await loadWebSocket();
        const socket = new WS(toWsUrl(wsPath), [], {
          WebSocket: window.WebSocket,
          maxRetries: 10,
          reconnectInterval: 1500,
          connectionTimeout: 5000,
          maxReconnectionDelay: 5000,
          minReconnectionDelay: 500,
          debug: false
        });
        wsRef.current = socket;

        socket.addEventListener("message", (event) => {
          if (!mounted) return;
          try {
            const data = JSON.parse(event.data as string) as LeaderboardItem[];
            setLeaderboard(data);
          } catch {
            // ignore malformed messages
          }
        });

        socket.addEventListener("open", () => {
          if (mounted) setWsConnected(true);
        });
        socket.addEventListener("close", () => {
          if (mounted) setWsConnected(false);
        });
        socket.addEventListener("error", () => {
          if (mounted) setWsConnected(false);
        });
      } catch (err) {
        console.error("WS init failed", err);
        if (mounted) setWsConnected(false);
      }
    }, 500);

    return () => {
      mounted = false;
      clearTimeout(connectTimer);
      wsRef.current?.close();
      wsRef.current = null;
    };
  }, [user, token]);

  useEffect(() => {
    if (!authStarted && initData) {
      setAuthStarted(true);
      void fetchToken();
    }
    return () => {
      if (refreshTimer.current) {
        clearTimeout(refreshTimer.current);
        refreshTimer.current = null;
      }
    };
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
    return () => {
      if (refreshTimer.current) {
        clearTimeout(refreshTimer.current);
        refreshTimer.current = null;
      }
    };
  }, [parsedExp, tokenExp, initData]);

  const fetchToken = async () => {
    setAuthInProgress(true);
    try {
      const res = await fetchWithTimeout(`${apiBase}/auth/telegram`, {
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
      sendDiag("auth-ok", undefined, { exp: data.expiresAt });
      return data.token;
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Unknown error";
      logError(msg);
      sendDiag("auth-error", msg);
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
    const res = await fetchWithTimeout(`${apiBase}${path}`, { ...init, headers });
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
      return fetchWithTimeout(`${apiBase}${path}`, { ...init, headers: retryHeaders });
    }
    return res;
  };

  const fetchProfile = async (overrideToken?: string) => {
    updateLoading("profile", true);
    try {
      const headers = new Headers();
      headers.set("Authorization", `Bearer ${overrideToken ?? token}`);
      let res = await fetchWithTimeout(`${apiBase}/auth/me`, { headers });
      if (res.status === 401 && initData) {
        const newToken = await fetchToken();
        if (newToken) {
          const retryHeaders = new Headers();
          retryHeaders.set("Authorization", `Bearer ${newToken}`);
          res = await fetchWithTimeout(`${apiBase}/auth/me`, { headers: retryHeaders });
        }
      }
      if (!res.ok) {
        throw new Error(`Profile fetch failed (${res.status})`);
      }
      const data = await res.json();
      setUser(data.user);
      if (data.user?.role !== "root") {
        setRootResults([]);
        setRootSearchQuery("");
      }
      const jobs: Promise<unknown>[] = [loadLeaderboard()];
      if (data.user?.role === "admin" || data.user?.role === "root") {
        jobs.push(loadHistory(overrideToken ?? token));
      }
      await Promise.allSettled(jobs);
      sendDiag("profile-ok");
    } finally {
      updateLoading("profile", false);
    }
  };

  const loadHistory = async (tokenOverride?: string) => {
    updateLoading("history", true);
    try {
      const res = await authFetch("/codes/admin/history", undefined, true, tokenOverride);
      if (!res.ok) return;
      const data = await res.json();
      setHistory(data);
    } finally {
      updateLoading("history", false);
    }
  };

  const loadLeaderboard = async () => {
    updateLoading("leaderboard", true);
    try {
      const cached = dataCache.get<LeaderboardItem[]>("leaderboard");
      if (cached) {
        setLeaderboard(cached);
        return;
      }
      await fetchWithRetry(async () => {
        const res = await fetchWithTimeout(`${apiBase}/codes/leaderboard`);
        if (!res.ok) throw new Error(`Leaderboard failed (${res.status})`);
        const data = await res.json();
        dataCache.set("leaderboard", data, 5000);
        setLeaderboard(data);
      });
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Не удалось загрузить рейтинг";
      logError(msg);
    } finally {
      updateLoading("leaderboard", false);
    }
  };

  const searchUsers = async () => {
    if (!rootSearchQuery.trim()) return;
    setRootBusy(true);
    setRootError(null);
    try {
      const res = await authFetch(`/root/users?query=${encodeURIComponent(rootSearchQuery.trim())}`);
      if (!res.ok) {
        throw new Error(`Search failed (${res.status})`);
      }
      const data = await res.json();
      setRootResults(data);
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Не удалось выполнить поиск";
      setRootError(msg);
    } finally {
      setRootBusy(false);
    }
  };

  const toggleAdmin = async (id: number, current: boolean) => {
    setRootBusy(true);
    setRootError(null);
    try {
      const res = await authFetch(`/root/users/${id}/admin`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ isAdmin: !current })
      });
      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.message || `Не удалось обновить роль (${res.status})`);
      }
      setRootResults((prev) =>
        prev.map((u) => (u.telegramId === id ? { ...u, isAdmin: !current || u.isRoot } : u))
      );
    } catch (err) {
      const msg = err instanceof Error ? err.message : "Не удалось обновить роль";
      setRootError(msg);
    } finally {
      setRootBusy(false);
    }
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
      dataCache.invalidate("leaderboard");
      await loadLeaderboard();
      const candiesText = `🍬: ${data.balance}`;
      setModal({
        title: "Успех",
        message: data.message ? `${data.message} ${candiesText}` : candiesText,
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
      dataCache.invalidate("history");
      await loadHistory();
      setModal({
        title: "Код создан",
        message: `Код: ${data.code} (+🍬${data.points}), истекает: ${new Date(data.expiresAt).toLocaleString()}`,
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
      {loading.profile && <div className="muted">Загрузка профиля...</div>}
      {loading.leaderboard && !loading.profile && <div className="muted">Загрузка рейтинга...</div>}
      {loading.history && user && (user.role === "admin" || user.role === "root") && (
        <div className="muted">Обновляем историю кодов...</div>
      )}

      {user && (
        <section className="card">
          <h2>Профиль</h2>
          <div className="user">
            {user.photo_url && <img src={user.photo_url} alt="avatar" width={64} height={64} />}
            <div>
              <div className="user-name">{displayName(user)}</div>
              <div className="muted">🍬 {user.balance}</div>
              <div className="muted">{user.role === "root" ? "Root" : user.role === "admin" ? "Админ" : "Клиент"}</div>
              {config?.tokenTtlSeconds ? <div className="muted">TTL токена: {config.tokenTtlSeconds}s</div> : null}
            </div>
          </div>
        </section>
      )}

      {user && (
        <Suspense fallback={<LoadingPage subtitle="Загрузка компонента..." />}>
          {user.role === "admin" || user.role === "root" ? (
            <AdminPage
              pointsToGenerate={pointsToGenerate}
              onPointsChange={setPointsToGenerate}
              onGenerate={generateCode}
              busy={busy}
              history={history}
              wsConnected={wsConnected}
              isRoot={user.role === "root"}
              rootSearchQuery={rootSearchQuery}
              onRootSearchChange={setRootSearchQuery}
              onRootSearch={searchUsers}
              rootResults={rootResults}
              onToggleAdmin={toggleAdmin}
              rootBusy={rootBusy}
              rootError={rootError}
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
          )}
        </Suspense>
      )}
    </div>
  );
}

export default App;
