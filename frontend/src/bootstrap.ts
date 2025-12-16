// Pre-React bootstrap: checks build version and cleans client caches if версия не совпадает.
// This runs before main.tsx is imported, чтобы не подгружать устаревшие бандлы.
declare const __APP_VERSION__: string | undefined;

const VERSION =
  (import.meta.env.VITE_APP_VERSION as string | undefined) ??
  (typeof __APP_VERSION__ !== "undefined" ? __APP_VERSION__ : "dev");
const VERSION_KEY = "ucode:buildVersion";

const clearClientCaches = async () => {
  try {
    if ("caches" in window) {
      const keys = await caches.keys();
      await Promise.all(keys.map((k) => caches.delete(k)));
    }
    sessionStorage.clear();
    localStorage.clear();
    if ("serviceWorker" in navigator) {
      const regs = await navigator.serviceWorker.getRegistrations();
      await Promise.all(regs.map((r) => r.unregister()));
    }
  } catch (err) {
    // Логируем, но не блокируем загрузку приложения
    console.error("Cache cleanup failed", err);
  }
};

const ensureFreshVersion = async () => {
  const saved = localStorage.getItem(VERSION_KEY);
  if (saved && saved !== VERSION) {
    await clearClientCaches();
  }
  localStorage.setItem(VERSION_KEY, VERSION);
};

const boot = async () => {
  await ensureFreshVersion();
  await import("./main");
};

void boot();
