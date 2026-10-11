<script setup lang="ts">
import { ref } from 'vue';
import { api, type Collection } from '../api/client';
import { useToast } from '../stores/toast';
import { FolderPlus, X, Folder, Globe, Link2, Lock } from '@lucide/vue';

const props = defineProps<{
  collection?: Collection | null;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved', col: Collection): void;
}>();

const { success, error: toastError } = useToast();

const name = ref(props.collection?.name || '');
const description = ref(props.collection?.description || '');
const visibility = ref<'public' | 'unlisted' | 'private'>(props.collection?.visibility || 'public');
const isLoading = ref(false);
const errorMessage = ref('');

const isEdit = !!props.collection;

async function handleSubmit() {
  if (!name.value.trim()) {
    errorMessage.value = 'Please provide a collection name';
    return;
  }

  isLoading.value = true;
  errorMessage.value = '';

  try {
    if (isEdit && props.collection) {
      await api.updateCollection(props.collection.id, {
        name: name.value.trim(),
        description: description.value.trim() || undefined,
        visibility: visibility.value,
      });
      success(`Collection "${name.value}" updated!`);
      emit('saved', {
        ...props.collection,
        name: name.value.trim(),
        description: description.value.trim() || null,
        visibility: visibility.value,
      });
    } else {
      const created = await api.createCollection({
        name: name.value.trim(),
        description: description.value.trim() || undefined,
        visibility: visibility.value,
      });
      success(`Collection "${created.name}" created!`);
      emit('saved', created);
    }
    emit('close');
  } catch (err: any) {
    errorMessage.value = err.message || 'Failed to save collection';
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
            <FolderPlus :size="20" />
          </div>
          <div>
            <h2>{{ isEdit ? 'Edit Collection' : 'Create New Collection' }}</h2>
            <p class="subtitle">Group clips by game, tournament, or theme</p>
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
          <label for="col-name">Collection Name</label>
          <input
            id="col-name"
            v-model="name"
            type="text"
            placeholder="e.g. Apex Season 25 Highlights"
            class="form-input"
            required
            autofocus
          />
        </div>

        <div class="form-group">
          <label for="col-desc">Description (optional)</label>
          <textarea
            id="col-desc"
            v-model="description"
            rows="3"
            placeholder="Brief summary of this highlight reel..."
            class="form-textarea"
          ></textarea>
        </div>

        <div class="form-group">
          <label>Visibility</label>
          <div class="vis-options">
            <label class="vis-pill-label" :class="{ active: visibility === 'public' }">
              <input type="radio" v-model="visibility" value="public" class="vis-radio" />
              <Globe :size="14" />
              <span>Public</span>
            </label>
            <label class="vis-pill-label" :class="{ active: visibility === 'unlisted' }">
              <input type="radio" v-model="visibility" value="unlisted" class="vis-radio" />
              <Link2 :size="14" />
              <span>Unlisted</span>
            </label>
            <label class="vis-pill-label" :class="{ active: visibility === 'private' }">
              <input type="radio" v-model="visibility" value="private" class="vis-radio" />
              <Lock :size="14" />
              <span>Private</span>
            </label>
          </div>
          <span class="vis-hint">
            {{
              visibility === 'public' ? 'Visible to everyone in the collections directory.' :
              visibility === 'unlisted' ? 'Hidden from directory; accessible only via direct share link.' :
              'Only visible to you (admin). Link is blocked for public viewers.'
            }}
          </span>
        </div>

        <div class="modal-actions">
          <button type="button" class="btn btn-secondary" @click="emit('close')">
            Cancel
          </button>
          <button type="submit" class="btn btn-primary" :disabled="isLoading">
            <Folder :size="16" />
            <span>{{ isLoading ? 'Saving...' : (isEdit ? 'Save Changes' : 'Create Collection') }}</span>
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
  margin-bottom: 1.25rem;

  .title-with-icon {
    display: flex;
    align-items: center;
    gap: 0.85rem;

    .icon-circle {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: rgba(139, 92, 246, 0.15);
      color: #c4b5fd;
      border: 1px solid rgba(139, 92, 246, 0.3);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    h2 {
      font-size: 1.15rem;
      font-weight: 700;
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

  .vis-options {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 0.5rem;
  }

  .vis-pill-label {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.45rem;
    padding: 0.5rem 0.65rem;
    border-radius: $radius-md;
    border: 1px solid $border-medium;
    background: $bg-input;
    color: $text-secondary;
    font-size: 0.82rem;
    font-weight: 600;
    cursor: pointer;
    transition: all $transition-fast;

    .vis-radio {
      display: none;
    }

    &:hover {
      background: $bg-surface-elevated;
      color: $text-primary;
    }

    &.active {
      background: rgba(139, 92, 246, 0.15);
      border-color: $accent-violet;
      color: #c4b5fd;
      box-shadow: 0 0 12px $accent-violet-glow;
    }
  }

  .vis-hint {
    font-size: 0.75rem;
    color: $text-muted;
    margin-top: 0.2rem;
    line-height: 1.4;
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
