import { ref, reactive } from 'vue';
import { api, type AuthStatus } from '../api/client';

const state = reactive<AuthStatus>({
  isAuthenticated: false,
  username: null,
});

const isInitialized = ref(false);

export function useAuth() {
  async function init() {
    if (isInitialized.value) return;
    try {
      const status = await api.getAuthStatus();
      state.isAuthenticated = status.isAuthenticated;
      state.username = status.username;
    } catch {
      state.isAuthenticated = false;
      state.username = null;
    } finally {
      isInitialized.value = true;
    }
  }

  async function login(username: string, pass: string) {
    const res = await api.login(username, pass);
    state.isAuthenticated = true;
    state.username = res.username;
    return res;
  }

  async function logout() {
    await api.logout();
    state.isAuthenticated = false;
    state.username = null;
  }

  return {
    state,
    isInitialized,
    init,
    login,
    logout,
  };
}
