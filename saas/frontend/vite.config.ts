import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Dev: same-origin /api via the Vite proxy (the SPA never hardcodes a
// host — in production nginx serves /api from the same origin).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": {
        target: "http://localhost:8080",
        changeOrigin: false,
      },
    },
  },
  build: {
    outDir: "dist",
    sourcemap: true,
  },
});
