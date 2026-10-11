<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { api, type Collection, type Clip } from '../api/client';
import { useToast } from '../stores/toast';
import { UploadCloud, Film, X, Check, AlertCircle } from '@lucide/vue';

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'uploaded', clip: Clip): void;
}>();

const { success, error: toastError } = useToast();

const fileInputRef = ref<HTMLInputElement | null>(null);
const selectedFile = ref<File | null>(null);
const title = ref('');
const description = ref('');
const selectedCollectionId = ref('');
const collections = ref<Collection[]>([]);

const isDragging = ref(false);
const isUploading = ref(false);
const uploadProgress = ref(0);
const errorMessage = ref('');

onMounted(async () => {
  try {
    collections.value = await api.getCollections();
  } catch (err) {
    console.error('Failed to fetch collections', err);
  }
});

function onFileSelect(e: Event) {
  const files = (e.target as HTMLInputElement).files;
  if (files && files.length > 0) {
    handleFile(files[0]);
  }
}

function onDrop(e: DragEvent) {
  isDragging.value = false;
  if (e.dataTransfer && e.dataTransfer.files.length > 0) {
    handleFile(e.dataTransfer.files[0]);
  }
}

function handleFile(file: File) {
  selectedFile.value = file;
  if (!title.value) {
    // Clean up filename for default title
    const raw = file.name.replace(/\.[^/.]+$/, '');
    title.value = raw.replace(/[_-]/g, ' ').trim();
  }
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

async function handleUpload() {
  if (!selectedFile.value) {
    errorMessage.value = 'Please select a video file';
    return;
  }

  isUploading.value = true;
  uploadProgress.value = 0;
  errorMessage.value = '';

  const formData = new FormData();
  formData.append('file', selectedFile.value);
  if (title.value) formData.append('title', title.value);
  if (description.value) formData.append('description', description.value);
  if (selectedCollectionId.value) formData.append('collectionId', selectedCollectionId.value);

  try {
    const newClip = await api.uploadClip(formData, (pct) => {
      uploadProgress.value = pct;
    });

    success(`Clip "${newClip.title}" uploaded successfully!`);
    emit('uploaded', newClip);
    emit('close');
  } catch (err: any) {
    errorMessage.value = err.message || 'Upload failed';
    toastError(errorMessage.value);
  } finally {
    isUploading.value = false;
  }
}
</script>

<template>
  <div class="modal-backdrop" @click.self="!isUploading && emit('close')">
    <div class="modal-card upload-modal">
      <div class="modal-header">
        <div class="title-with-icon">
          <div class="icon-circle">
            <UploadCloud :size="20" />
          </div>
          <div>
            <h2>Upload Video Game Clip</h2>
            <p class="subtitle">Directly publish a clip or drop it into your ingest folder</p>
          </div>
        </div>
        <button v-if="!isUploading" class="close-btn" @click="emit('close')">
          <X :size="18" />
        </button>
      </div>

      <div v-if="errorMessage" class="error-banner">
        <AlertCircle :size="16" />
        <span>{{ errorMessage }}</span>
      </div>

      <!-- Dropzone -->
      <div
        class="dropzone"
        :class="{ dragging: isDragging, 'has-file': !!selectedFile }"
        @dragover.prevent="isDragging = true"
        @dragleave.prevent="isDragging = false"
        @drop.prevent="onDrop"
        @click="fileInputRef?.click()"
      >
        <input
          ref="fileInputRef"
          type="file"
          accept="video/mp4,video/webm,video/mkv,video/quicktime,video/x-matroska"
          class="file-input-hidden"
          @change="onFileSelect"
        />

        <template v-if="selectedFile">
          <Film :size="36" class="dropzone-icon selected" />
          <div class="file-info">
            <span class="file-name">{{ selectedFile.name }}</span>
            <span class="file-meta">{{ formatBytes(selectedFile.size) }} • Click to change file</span>
          </div>
        </template>
        <template v-else>
          <UploadCloud :size="40" class="dropzone-icon" />
          <div class="dropzone-text">
            <p class="dropzone-main">Drag and drop your gaming clip here</p>
            <p class="dropzone-sub">Supports MP4, WebM, MKV, MOV (up to 2GB)</p>
          </div>
        </template>
      </div>

      <!-- Progress bar -->
      <div v-if="isUploading" class="progress-section">
        <div class="progress-bar-container">
          <div class="progress-bar-fill" :style="{ width: `${uploadProgress}%` }"></div>
        </div>
        <div class="progress-label">
          <span>Uploading clip...</span>
          <span>{{ uploadProgress }}%</span>
        </div>
      </div>

      <!-- Metadata Form -->
      <form class="metadata-form" @submit.prevent="handleUpload">
        <div class="form-group">
          <label for="clip-title">Title</label>
          <input
            id="clip-title"
            v-model="title"
            type="text"
            placeholder="e.g. Ace clutch on Haven"
            class="form-input"
            required
            :disabled="isUploading"
          />
        </div>

        <div class="form-group">
          <label for="clip-desc">Description (optional)</label>
          <textarea
            id="clip-desc"
            v-model="description"
            rows="2"
            placeholder="Game details, loadout, timestamp..."
            class="form-textarea"
            :disabled="isUploading"
          ></textarea>
        </div>

        <div class="form-group">
          <label for="clip-col">Add to Collection (optional)</label>
          <select
            id="clip-col"
            v-model="selectedCollectionId"
            class="form-select"
            :disabled="isUploading"
          >
            <option value="">None (Individual clip)</option>
            <option v-for="col in collections" :key="col.id" :value="col.id">
              {{ col.name }} ({{ col.clipCount }} clips)
            </option>
          </select>
        </div>

        <div class="modal-actions">
          <button
            type="button"
            class="btn btn-secondary"
            :disabled="isUploading"
            @click="emit('close')"
          >
            Cancel
          </button>
          <button
            type="submit"
            class="btn btn-primary"
            :disabled="!selectedFile || isUploading"
          >
            <UploadCloud :size="16" />
            <span>{{ isUploading ? 'Uploading...' : 'Publish Clip' }}</span>
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
  background: rgba(5, 7, 12, 0.82);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.upload-modal {
  max-width: 580px;
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
      width: 42px;
      height: 42px;
      border-radius: 50%;
      background: rgba(0, 242, 254, 0.12);
      color: $accent-cyan;
      border: 1px solid rgba(0, 242, 254, 0.25);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    h2 {
      font-size: 1.2rem;
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

.error-banner {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.65rem 0.9rem;
  background: rgba($accent-rose, 0.15);
  border: 1px solid rgba($accent-rose, 0.3);
  border-radius: $radius-md;
  color: #fda4af;
  font-size: 0.83rem;
  margin-bottom: 1rem;
}

.dropzone {
  border: 2px dashed $border-medium;
  border-radius: $radius-lg;
  padding: 1.75rem 1rem;
  text-align: center;
  cursor: pointer;
  background: rgba(18, 22, 33, 0.6);
  transition: all $transition-smooth;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;

  &:hover, &.dragging {
    border-color: $accent-cyan;
    background: rgba(0, 242, 254, 0.05);
    box-shadow: 0 0 20px $accent-cyan-glow;

    .dropzone-icon {
      color: $accent-cyan;
      transform: scale(1.08);
    }
  }

  &.has-file {
    border-color: rgba($accent-emerald, 0.5);
    background: rgba(16, 185, 129, 0.05);
  }

  .dropzone-icon {
    color: $text-muted;
    transition: all $transition-fast;

    &.selected {
      color: $accent-emerald;
    }
  }

  .dropzone-text {
    .dropzone-main {
      font-size: 0.95rem;
      font-weight: 600;
      color: $text-primary;
    }

    .dropzone-sub {
      font-size: 0.8rem;
      color: $text-muted;
      margin-top: 0.2rem;
    }
  }

  .file-info {
    display: flex;
    flex-direction: column;
    gap: 0.2rem;

    .file-name {
      font-weight: 600;
      font-size: 0.95rem;
      color: $text-primary;
    }
    .file-meta {
      font-size: 0.78rem;
      color: $text-secondary;
    }
  }

  .file-input-hidden {
    display: none;
  }
}

.progress-section {
  margin-top: 1rem;

  .progress-bar-container {
    height: 8px;
    background: rgba(255, 255, 255, 0.1);
    border-radius: 4px;
    overflow: hidden;

    .progress-bar-fill {
      height: 100%;
      background: linear-gradient(90deg, $accent-cyan, $accent-emerald);
      transition: width 0.15s ease;
    }
  }

  .progress-label {
    display: flex;
    justify-content: space-between;
    font-size: 0.78rem;
    color: $text-secondary;
    margin-top: 0.35rem;
  }
}

.metadata-form {
  margin-top: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;

  .form-group {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;

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
