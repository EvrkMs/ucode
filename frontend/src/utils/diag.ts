const endpoint = `${(import.meta.env.VITE_API_BASE ?? "/api").replace(/\/$/, "")}/diag/client`;
const start = performance.now();

type Extra = Record<string, unknown> | null | undefined;

export const sendDiag = (type: string, detail?: string, extra?: Extra) => {
  const payload = {
    type,
    detail,
    ua: navigator.userAgent,
    t: Math.round(performance.now() - start),
    extra
  };

  try {
    const body = JSON.stringify(payload);
    if (navigator.sendBeacon) {
      const blob = new Blob([body], { type: "application/json" });
      navigator.sendBeacon(endpoint, blob);
      return;
    }
    fetch(endpoint, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body,
      keepalive: true
    }).catch(() => {});
  } catch (err) {
    console.warn("sendDiag failed", err);
  }
};
