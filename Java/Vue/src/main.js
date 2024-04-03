import { createApp } from 'vue'
import App from './App.vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import HomeComponent from './components/Home.vue'
import AboutComponent from './components/About.vue'

const routes = [
    { path: '/', component: HomeComponent },
    { path: '/about', component: AboutComponent },
]

const router = createRouter({ history: createWebHashHistory(), routes })

createApp(App).use(router).mount('#app')
