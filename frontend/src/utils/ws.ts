export const toWsUrl = (path: string) => {
  const base = path.startsWith("http") ? path : `${window.location.origin}${path}`;
  return base.replace(/^http/i, "ws");
};
