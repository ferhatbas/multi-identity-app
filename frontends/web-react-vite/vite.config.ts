import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));

const apiTarget = process.env.API_TARGET || 'http://localhost:5231';

export default defineConfig({
  plugins: [react()],
  resolve: { alias: { '@': path.resolve(here, 'src') } },
  server: {
    port: 3040,
    strictPort: true,
    host: process.env.VITE_HOST || undefined,
    watch: process.env.VITE_USE_POLLING === 'true' ? { usePolling: true, interval: 300 } : undefined,
    proxy: { '/api': { target: apiTarget, changeOrigin: true } },
  },
});
