// Pre-React bootstrap: checks build version and cleans client caches if версия не совпадает.
// This runs before main.tsx is imported, чтобы не подгружать устаревшие бандлы.
declare const __APP_VERSION__: string | undefined;

const VERSION =
  (import.meta.env.VITE_APP_VERSION as string | undefined) ??
  (typeof __APP_VERSION__ !== "undefined" ? __APP_VERSION__ : "dev");
const VERSION_KEY = "ucode:buildVersion";

const ensureFreshVersion = () => {
  try {
    const saved = localStorage.getItem(VERSION_KEY);
    if (saved !== VERSION) {
      // Просто обновляем версию; кеш/сложная очистка убраны, чтобы не блокировать iOS WebView.
      localStorage.setItem(VERSION_KEY, VERSION);
    }
  } catch (err) {
    console.error("Version check failed", err);
  }
};

const boot = async () => {
  ensureFreshVersion();
  await import("./main");
};

void boot();
