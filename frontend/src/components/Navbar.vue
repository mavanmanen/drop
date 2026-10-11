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
          <Upload :size="15" />
          <span>Upload</span>
        </button>

        <!-- Auth action -->
        <template v-if="auth.isAuthenticated">
          <div class="user-chip">
            <span class="user-name">{{ auth.username }}</span>
            <button class="btn btn-ghost btn-icon" title="Logout" @click="logout">
              <LogOut :size="15" />
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
  background: $bg-root;
  border-bottom: 1px solid $border-subtle;
  padding: 0.65rem 1.5rem;
}

.nav-container {
  max-width: 1400px;
  margin: 0 auto;
  display: flex;
  align-items: center;
  gap: 1.25rem;
}

.brand {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  text-decoration: none;
  color: $text-primary;

  .logo-box {
    width: 28px;
    height: 28px;
    border-radius: $radius-sm;
    background: #2563eb;
    display: flex;
    align-items: center;
    justify-content: center;
    color: #ffffff;
  }

  .brand-text {
    font-size: 1.15rem;
    font-weight: 700;
    letter-spacing: -0.02em;
    color: $text-primary;
  }

  .brand-tag {
    font-size: 0.65rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    padding: 0.1rem 0.35rem;
    border-radius: $radius-xs;
    background: rgba(255, 255, 255, 0.08);
    color: $text-secondary;
    border: 1px solid $border-subtle;
  }
}

.nav-links {
  display: flex;
  align-items: center;
  gap: 0.25rem;

  .nav-link {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    padding: 0.35rem 0.65rem;
    border-radius: $radius-sm;
    color: $text-secondary;
    font-size: 0.85rem;
    font-weight: 500;
    text-decoration: none;
    transition: color $transition-fast, background-color $transition-fast;

    &:hover {
      color: $text-primary;
      background: rgba(255, 255, 255, 0.05);
    }

    &.active {
      color: $text-primary;
      background: rgba(255, 255, 255, 0.08);
      font-weight: 600;
    }
  }
}

.search-wrapper {
  flex: 1;
  max-width: 320px;
  position: relative;
  display: flex;
  align-items: center;

  .search-icon {
    position: absolute;
    left: 0.65rem;
    color: $text-muted;
    pointer-events: none;
  }

  .search-input {
    width: 100%;
    background: $bg-surface;
    border: 1px solid $border-medium;
    border-radius: $radius-sm;
    padding: 0.35rem 0.75rem 0.35rem 2rem;
    color: $text-primary;
    font-size: 0.825rem;
    transition: border-color $transition-fast;

    &:focus {
      outline: none;
      border-color: #3b82f6;
      background: $bg-input;
    }

    &::placeholder {
      color: $text-muted;
    }
  }
}

.nav-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-left: auto;

  .btn-upload, .btn-login {
    height: 32px;
    padding: 0 0.75rem;
    font-size: 0.825rem;
  }
}

.user-chip {
  height: 32px;
  display: inline-flex;
  align-items: center;
  gap: 0.35rem;
  padding: 0 0.35rem 0 0.65rem;
  background: $bg-surface-elevated;
  border: 1px solid $border-medium;
  border-radius: $radius-md;
  font-size: 0.825rem;

  .user-name {
    font-weight: 500;
    color: $text-primary;
  }

  .btn-icon {
    width: 24px;
    height: 24px;
    padding: 0;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: $radius-sm;
    color: $text-muted;

    &:hover {
      color: $text-primary;
      background: rgba(255, 255, 255, 0.08);
    }
  }
}

@media (max-width: 820px) {
  .search-wrapper {
    display: none;
  }
}
</style>
