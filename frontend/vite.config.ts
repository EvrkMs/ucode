import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import pkg from "./package.json";

const gitSha =
  process.env.VITE_GIT_SHA ||
  process.env.GIT_COMMIT ||
  process.env.VERCEL_GIT_COMMIT_SHA ||
  process.env.CF_PAGES_COMMIT_SHA ||
  process.env.COMMIT_SHA;
const appVersion = process.env.VITE_APP_VERSION || (gitSha ? `${pkg.version}-${gitSha.slice(0, 7)}` : pkg.version);

export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(appVersion)
  },
  build: {
    // Включаем минификацию
    minify: "terser",
    terserOptions: {
      compress: {
        drop_console: true, // Убираем console.log в продакшене
        drop_debugger: true,
        pure_funcs: ["console.log", "console.info"], // Убираем конкретные функции
      },
    },
    // Уменьшаем размер чанков
    chunkSizeWarningLimit: 500,
    rollupOptions: {
      output: {
        // Разделяем на чанки для лучшего кеширования
        manualChunks: {
          // React и ReactDOM в отдельный чанк
          react: ["react", "react-dom"],
          // Вебсокет отдельно (загружается только когда нужен)
          websocket: ["reconnecting-websocket"],
        },
        entryFileNames: "assets/[name].[hash].js",
        chunkFileNames: "assets/[name].[hash].js",
        assetFileNames: "assets/[name].[hash].[ext]"
      }
    },
    // CSS code splitting для параллельной загрузки
    cssCodeSplit: true,
    // Оптимизация ассетов
    assetsInlineLimit: 4096, // Инлайним маленькие файлы (<4kb)
    // Отключаем sourcemaps в продакшене для уменьшения размера
    sourcemap: false,
  },
  server: {
    port: 5173,
    host: true
  },
  // Оптимизация зависимостей
  optimizeDeps: {
    include: ["react", "react-dom", "reconnecting-websocket"],
  },
});