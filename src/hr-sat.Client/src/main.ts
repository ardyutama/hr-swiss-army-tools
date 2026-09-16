import { createApp } from 'vue'
import ui from '@nuxt/ui/vue-plugin'

import App from './App.vue'
import { router } from './router'
import { installTechnicalFailureHandling } from './technical-failure/technicalFailure'
import './style.css'

const app = createApp(App).use(router).use(ui)

installTechnicalFailureHandling(app, router)
app.mount('#app')

