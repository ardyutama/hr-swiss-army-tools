import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vitest/config';
import plugin from '@vitejs/plugin-vue';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [plugin()],
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url))
        }
    },
    server: {
        proxy: {
            '/api': {
                target: 'http://localhost:5086'
            }
        },
        port: 5173
    },
    test: {
        environment: 'jsdom'
    }
})
