<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuth } from '../stores/auth';
import {
  Film,
  Folder,
  Upload,
  LogIn,
  LogOut,
  Search,
  Gamepad2,
  HardDrive
} from '@lucide/vue';

const router = useRouter();
const { state: auth, logout } = useAuth();

const emit = defineEmits<{
  (e: 'open-login'): void;
  (e: 'open-upload'): void;
  (e: 'search', query: string): void;
}>();

const searchQuery = ref('');

function onSearch() {
  emit('search', searchQuery.value);
  if (router.currentRoute.value.path !== '/') {
    router.push({ path: '/', query: searchQuery.value ? { q: searchQuery.value } : {} });
  }
}
</script>

<template>
  <header class="navbar">
    <div class="nav-container">
      <!-- Brand -->
      <router-link to="/" class="brand">
        <div class="logo-box">
          <Gamepad2 class="logo-icon" :size="22" />
        </div>
        <span class="brand-text">drop</span>
        <span class="brand-tag">clips</span>
      </router-link>

      <!-- Nav links -->
      <nav class="nav-links">
        <router-link to="/" class="nav-link" active-class="active">
          <Film :size="16" />
          <span>Clips</span>
        </router-link>
        <router-link to="/collections" class="nav-link" active-class="active">
          <Folder :size="16" />
          <span>Collections</span>
        </router-link>
      </nav>

      <!-- Search Bar -->
      <div class="search-wrapper">
        <Search class="search-icon" :size="16" />
        <input
          v-model="searchQuery"
          type="search"
          placeholder="Search clips..."
          class="search-input"
          @input="onSearch"
          @keydown.enter="onSearch"
        />
      </div>

      <!-- Action items -->
      <div class="nav-actions">
        <!-- Admin Upload button -->
        <button
          v-if="auth.isAuthenticated"
          class="btn btn-primary btn-upload"
          @click="emit('open-upload')"
        >
          <Upload :size="16" />
          <span>Upload</span>
        </button>

        <!-- Ingest status badge -->
        <div class="ingest-indicator" title="Ingest folder watcher active">
          <span class="pulse-dot"></span>
          <span class="ingest-label">Ingest Active</span>
        </div>

        <!-- Auth action -->
        <template v-if="auth.isAuthenticated">
          <div class="user-chip">
            <span class="user-name">{{ auth.username }}</span>
            <button class="btn btn-ghost btn-icon" title="Logout" @click="logout">
              <LogOut :size="16" />
            </button>
          </div>
        </template>
        <template v-else>
          <button class="btn btn-secondary btn-login" @click="emit('open-login')">
            <LogIn :size="15" />
            <span>Admin Login</span>
          </button>
        </template>
      </div>
    </div>
  </header>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.navbar {
  position: sticky;
  top: 0;
  z-index: 100;
  background: rgba(11, 13, 19, 0.85);
  backdrop-filter: $backdrop-blur;
  border-bottom: 1px solid $border-subtle;
  padding: 0.75rem 1.5rem;
}

.nav-container {
  max-width: 1400px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  text-decoration: none;
  color: $text-primary;

  .logo-box {
    width: 36px;
    height: 36px;
    border-radius: $radius-md;
    background: linear-gradient(135deg, $accent-cyan 0%, $accent-violet 100%);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #050811;
    box-shadow: 0 4px 12px $accent-cyan-glow;
  }

  .brand-text {
    font-size: 1.35rem;
    font-weight: 800;
    letter-spacing: -0.04em;
    background: linear-gradient(135deg, #ffffff 40%, $accent-cyan 100%);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
  }

  .brand-tag {
    font-size: 0.7rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.08em;
    padding: 0.15rem 0.4rem;
    border-radius: 4px;
    background: rgba(0, 242, 254, 0.12);
    color: $accent-cyan;
    border: 1px solid rgba(0, 242, 254, 0.25);
  }
}

.nav-links {
  display: flex;
  align-items: center;
  gap: 0.5rem;

  .nav-link {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    padding: 0.45rem 0.85rem;
    border-radius: $radius-md;
    color: $text-secondary;
    font-size: 0.875rem;
    font-weight: 500;
    text-decoration: none;
    transition: all $transition-fast;

    &:hover {
      color: $text-primary;
      background: rgba(255, 255, 255, 0.05);
    }

    &.active {
      color: $accent-cyan;
      background: rgba(0, 242, 254, 0.08);
      font-weight: 600;
    }
  }
}

.search-wrapper {
  flex: 1;
  max-width: 360px;
  position: relative;
  display: flex;
  align-items: center;

  .search-icon {
    position: absolute;
    left: 0.75rem;
    color: $text-muted;
    pointer-events: none;
  }

  .search-input {
    width: 100%;
    background: $bg-surface;
    border: 1px solid $border-subtle;
    border-radius: 20px;
    padding: 0.45rem 0.9rem 0.45rem 2.2rem;
    color: $text-primary;
    font-size: 0.85rem;
    transition: all $transition-fast;

    &:focus {
      outline: none;
      border-color: $accent-cyan;
      background: $bg-surface-elevated;
      box-shadow: 0 0 0 3px rgba(0, 242, 254, 0.12);
    }

    &::placeholder {
      color: $text-muted;
    }
  }
}

.nav-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-left: auto;
}

.ingest-indicator {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.35rem 0.65rem;
  border-radius: 20px;
  background: rgba(16, 185, 129, 0.08);
  border: 1px solid rgba(16, 185, 129, 0.2);
  font-size: 0.75rem;
  color: #a7f3d0;

  .pulse-dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: $accent-emerald;
    box-shadow: 0 0 8px $accent-emerald;
    animation: pulseGlow 2s infinite;
  }
}

@keyframes pulseGlow {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.5; transform: scale(0.85); }
}

.user-chip {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.25rem 0.5rem 0.25rem 0.75rem;
  background: $bg-surface-elevated;
  border: 1px solid $border-medium;
  border-radius: 20px;
  font-size: 0.85rem;

  .user-name {
    font-weight: 600;
    color: $text-primary;
  }

  .btn-icon {
    padding: 0.25rem;
    border-radius: 50%;
  }
}

@media (max-width: 820px) {
  .search-wrapper, .ingest-indicator {
    display: none;
  }
}
</style>
