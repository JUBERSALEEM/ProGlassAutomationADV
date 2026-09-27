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
  preview: {
    host: true,
    // Sandbox/CI previews proxy arbitrary generated hosts (*.e2b.app);
    // a locked-down production edge never runs `vite preview`.
    allowedHosts: true,
  },
  build: {
    outDir: "dist",
    sourcemap: true,
  },
});
