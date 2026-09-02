import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vitest/config';
import plugin from '@vitejs/plugin-vue';
import ui from '@nuxt/ui/vite';

// https://vitejs.dev/config/
export default defineConfig({
    plugins: [
        plugin(),
        ui({
            colorMode: false,
            ui: {
                colors: {
                    primary: 'blue',
                    neutral: 'zinc'
                }
            }
        })
    ],
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
