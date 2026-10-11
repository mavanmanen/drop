import { ref } from 'vue';

export interface ToastMessage {
  id: number;
  text: string;
  type: 'success' | 'error' | 'info';
}

const toasts = ref<ToastMessage[]>([]);
let nextId = 1;

export function useToast() {
  function show(text: string, type: 'success' | 'error' | 'info' = 'info', duration = 3000) {
    const id = nextId++;
    toasts.value.push({ id, text, type });

    setTimeout(() => {
      toasts.value = toasts.value.filter((t) => t.id !== id);
    }, duration);
  }

  function success(text: string, duration = 3000) {
    show(text, 'success', duration);
  }

  function error(text: string, duration = 4000) {
    show(text, 'error', duration);
  }

  return {
    toasts,
    show,
    success,
    error,
  };
}
