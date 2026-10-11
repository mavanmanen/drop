import { createRouter, createWebHistory } from 'vue-router';
import HomeView from '../views/HomeView.vue';
import ClipDetailView from '../views/ClipDetailView.vue';
import CollectionsView from '../views/CollectionsView.vue';
import CollectionDetailView from '../views/CollectionDetailView.vue';

const routes = [
  {
    path: '/',
    name: 'home',
    component: HomeView,
  },
  {
    path: '/clip/:id',
    name: 'clip-detail',
    component: ClipDetailView,
  },
  {
    path: '/collections',
    name: 'collections',
    component: CollectionsView,
  },
  {
    path: '/collection/:id',
    name: 'collection-detail',
    component: CollectionDetailView,
  },
  {
    path: '/:pathMatch(.*)*',
    redirect: '/',
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes,
  scrollBehavior() {
    return { top: 0 };
  },
});

export default router;
