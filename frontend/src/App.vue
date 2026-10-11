<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import Navbar from './components/Navbar.vue';
import LoginModal from './components/LoginModal.vue';
import UploadModal from './components/UploadModal.vue';
import { useAuth } from './stores/auth';
import { useToast } from './stores/toast';
import type { Clip } from './api/client';
import { CheckCircle2, AlertCircle, Info } from '@lucide/vue';

const router = useRouter();
const { init: initAuth } = useAuth();
const { toasts } = useToast();

const showLoginModal = ref(false);
const showUploadModal = ref(false);

onMounted(() => {
  initAuth();
});

function onUploaded(clip: Clip) {
  router.push(`/clip/${clip.id}`);
}
</script>

<template>
  <div class="drop-app">
    <Navbar
      @open-login="showLoginModal = true"
      @open-upload="showUploadModal = true"
    />

    <main class="main-content">
      <router-view />
    </main>

    <!-- Modals -->
    <LoginModal
      v-if="showLoginModal"
      @close="showLoginModal = false"
    />

    <UploadModal
      v-if="showUploadModal"
      @close="showUploadModal = false"
      @uploaded="onUploaded"
    />

    <!-- Global Toast Notifications -->
    <div class="toast-container">
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="toast"
        :class="`toast-${toast.type}`"
      >
        <CheckCircle2 v-if="toast.type === 'success'" :size="16" class="toast-icon" />
        <AlertCircle v-else-if="toast.type === 'error'" :size="16" class="toast-icon" />
        <Info v-else :size="16" class="toast-icon" />
        <span>{{ toast.text }}</span>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
.drop-app {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
}

.main-content {
  flex: 1;
  display: flex;
  flex-direction: column;
}
</style>
