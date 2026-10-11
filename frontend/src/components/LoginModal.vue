<script setup lang="ts">
import { ref } from 'vue';
import { useAuth } from '../stores/auth';
import { useToast } from '../stores/toast';
import { Lock, LogIn, X } from '@lucide/vue';

const emit = defineEmits<{
  (e: 'close'): void;
}>();

const { login } = useAuth();
const { success, error: toastError } = useToast();

const username = ref('');
const password = ref('');
const isLoading = ref(false);
const errorMessage = ref('');

async function handleSubmit() {
  if (!username.value || !password.value) {
    errorMessage.value = 'Please enter both username and password';
    return;
  }

  isLoading.value = true;
  errorMessage.value = '';

  try {
    await login(username.value, password.value);
    success(`Logged in as ${username.value}`);
    emit('close');
  } catch (err: any) {
    errorMessage.value = err.message || 'Invalid username or password';
    toastError(errorMessage.value);
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <div class="modal-backdrop" @click.self="emit('close')">
    <div class="modal-card">
      <div class="modal-header">
        <div class="title-with-icon">
          <div class="icon-circle">
            <Lock :size="18" />
          </div>
          <div>
            <h2>Admin Login</h2>
            <p class="subtitle">Enter the credentials configured for drop</p>
          </div>
        </div>
        <button class="close-btn" @click="emit('close')">
          <X :size="18" />
        </button>
      </div>

      <form class="modal-form" @submit.prevent="handleSubmit">
        <div v-if="errorMessage" class="error-banner">
          {{ errorMessage }}
        </div>

        <div class="form-group">
          <label for="admin-user">Username</label>
          <input
            id="admin-user"
            v-model="username"
            type="text"
            placeholder="admin"
            class="form-input"
            autocomplete="username"
            required
          />
        </div>

        <div class="form-group">
          <label for="admin-pass">Password</label>
          <input
            id="admin-pass"
            v-model="password"
            type="password"
            placeholder="••••••••"
            class="form-input"
            autocomplete="current-password"
            required
          />
        </div>

        <div class="modal-actions">
          <button type="button" class="btn btn-secondary" @click="emit('close')">
            Cancel
          </button>
          <button type="submit" class="btn btn-primary" :disabled="isLoading">
            <LogIn :size="16" />
            <span>{{ isLoading ? 'Authenticating...' : 'Sign In' }}</span>
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(5, 7, 12, 0.78);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  margin-bottom: 1.5rem;

  .title-with-icon {
    display: flex;
    align-items: center;
    gap: 0.85rem;

    .icon-circle {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: rgba(0, 242, 254, 0.12);
      color: $accent-cyan;
      border: 1px solid rgba(0, 242, 254, 0.25);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    h2 {
      font-size: 1.15rem;
      font-weight: 700;
      color: $text-primary;
    }

    .subtitle {
      font-size: 0.8rem;
      color: $text-secondary;
      margin-top: 0.1rem;
    }
  }

  .close-btn {
    background: transparent;
    border: none;
    color: $text-muted;
    cursor: pointer;
    padding: 0.3rem;
    border-radius: $radius-sm;

    &:hover {
      color: $text-primary;
      background: rgba(255, 255, 255, 0.08);
    }
  }
}

.modal-form {
  display: flex;
  flex-direction: column;
  gap: 1.1rem;

  .error-banner {
    padding: 0.65rem 0.9rem;
    background: rgba($accent-rose, 0.15);
    border: 1px solid rgba($accent-rose, 0.3);
    border-radius: $radius-md;
    color: #fda4af;
    font-size: 0.83rem;
  }

  .form-group {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;

    label {
      font-size: 0.83rem;
      font-weight: 600;
      color: $text-secondary;
    }
  }
}

.modal-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.75rem;
  margin-top: 0.5rem;
}
</style>
