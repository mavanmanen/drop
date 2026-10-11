<script setup lang="ts">
import { ref, computed, watch, onMounted, onBeforeUnmount } from 'vue';
import { api, type Clip, type Collection, type Visibility } from '../api/client';
import { useToast } from '../stores/toast';
import {
  Scissors,
  X,
  Play,
  Pause,
  RotateCcw,
  Sparkles,
  Lock,
  Globe,
  Link2,
  FolderPlus,
  ArrowRight
} from '@lucide/vue';

const props = defineProps<{
  show: boolean;
  sourceClip: Clip;
  allCollections: Collection[];
  initialCurrentTime?: number;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'trimmed', newClip: Clip): void;
}>();

const { success, error: toastError } = useToast();

const previewVideo = ref<HTMLVideoElement | null>(null);
const isPreviewPlaying = ref(false);
const previewTime = ref(0);
const duration = ref(props.sourceClip.durationSeconds || 60);

const startTime = ref(0);
const endTime = ref(Math.min(props.sourceClip.durationSeconds || 60, 30));
const trimTitle = ref('');
const trimDescription = ref('');
const trimVisibility = ref<Visibility>(props.sourceClip.visibility || 'public');
const selectedCollectionId = ref('');
const isSubmitting = ref(false);

const cutDuration = computed(() => {
  return Math.max(0, endTime.value - startTime.value);
});

function formatTime(seconds: number): string {
  if (isNaN(seconds) || seconds < 0) return '0:00.0';
  const m = Math.floor(seconds / 60);
  const s = (seconds % 60).toFixed(1);
  return `${m}:${parseFloat(s) < 10 ? '0' : ''}${s}`;
}

function initDefaults() {
  const sourceDuration = props.sourceClip.durationSeconds || 60;
  duration.value = sourceDuration;

  const initial = props.initialCurrentTime !== undefined && props.initialCurrentTime < sourceDuration
    ? Math.max(0, props.initialCurrentTime)
    : 0;

  startTime.value = Number(initial.toFixed(1));
  endTime.value = Number(Math.min(sourceDuration, startTime.value + 15).toFixed(1));
  if (endTime.value <= startTime.value) {
    startTime.value = Math.max(0, endTime.value - 5);
  }

  trimTitle.value = `${props.sourceClip.title} (Short)`;
  trimDescription.value = props.sourceClip.description || '';
  trimVisibility.value = props.sourceClip.visibility || 'public';
  selectedCollectionId.value = '';
}

watch(
  () => props.show,
  (val) => {
    if (val) {
      initDefaults();
    } else {
      if (previewVideo.value) {
        previewVideo.value.pause();
      }
    }
  },
  { immediate: true }
);

function setStartToCurrent() {
  if (previewTime.value < endTime.value) {
    startTime.value = Number(previewTime.value.toFixed(1));
  } else {
    startTime.value = Number(Math.max(0, endTime.value - 1).toFixed(1));
  }
}

function setEndToCurrent() {
  if (previewTime.value > startTime.value) {
    endTime.value = Number(Math.min(duration.value, previewTime.value).toFixed(1));
  } else {
    endTime.value = Number(Math.min(duration.value, startTime.value + 1).toFixed(1));
  }
}

function seekToStart() {
  if (previewVideo.value) {
    previewVideo.value.currentTime = startTime.value;
    previewVideo.value.play();
  }
}

function togglePreviewPlay() {
  if (!previewVideo.value) return;
  if (previewVideo.value.paused) {
    if (previewVideo.value.currentTime < startTime.value || previewVideo.value.currentTime >= endTime.value) {
      previewVideo.value.currentTime = startTime.value;
    }
    previewVideo.value.play();
  } else {
    previewVideo.value.pause();
  }
}

function onPreviewTimeUpdate() {
  if (!previewVideo.value) return;
  previewTime.value = previewVideo.value.currentTime;

  // Auto-loop within trim boundaries
  if (previewVideo.value.currentTime >= endTime.value) {
    previewVideo.value.currentTime = startTime.value;
    previewVideo.value.play();
  }
}

function onPreviewLoadedMetadata() {
  if (previewVideo.value && previewVideo.value.duration && !isNaN(previewVideo.value.duration)) {
    duration.value = previewVideo.value.duration;
    if (endTime.value > duration.value) {
      endTime.value = Number(duration.value.toFixed(1));
    }
  }
}

async function handleTrim() {
  if (cutDuration.value <= 0.1) {
    toastError('Clip duration must be at least 0.2 seconds.');
    return;
  }

  isSubmitting.value = true;
  try {
    const trimmed = await api.trimClip(props.sourceClip.id, {
      startSeconds: startTime.value,
      endSeconds: endTime.value,
      title: trimTitle.value.trim() || undefined,
      description: trimDescription.value.trim() || undefined,
      visibility: trimVisibility.value,
      collectionId: selectedCollectionId.value || undefined,
    });

    success(`Cut clip "${trimmed.title}" created successfully!`);
    emit('trimmed', trimmed);
    emit('close');
  } catch (err: any) {
    toastError(err.message || 'Failed to cut clip.');
  } finally {
    isSubmitting.value = false;
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Escape' && props.show) {
    emit('close');
  }
}

onMounted(() => {
  window.addEventListener('keydown', onKeydown);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKeydown);
});
</script>

<template>
  <div v-if="show" class="modal-backdrop" @click.self="emit('close')">
    <div class="modal-dialog trim-dialog">
      <div class="modal-header">
        <div class="modal-title-wrap">
          <Scissors :size="20" class="modal-icon" />
          <h2 class="modal-title">Cut Shorter Clip</h2>
        </div>
        <button class="btn-close" aria-label="Close" @click="emit('close')">
          <X :size="18" />
        </button>
      </div>

      <div class="modal-body trim-body">
        <!-- Video Preview Section -->
        <div class="preview-panel">
          <div class="video-preview-wrapper">
            <video
              ref="previewVideo"
              class="trim-preview-video"
              :src="sourceClip.videoUrl"
              playsinline
              @click="togglePreviewPlay"
              @play="isPreviewPlaying = true"
              @pause="isPreviewPlaying = false"
              @timeupdate="onPreviewTimeUpdate"
              @loadedmetadata="onPreviewLoadedMetadata"
            ></video>

            <button class="play-overlay-btn" @click="togglePreviewPlay">
              <Pause v-if="isPreviewPlaying" :size="28" />
              <Play v-else :size="28" class="play-offset" />
            </button>
          </div>

          <div class="preview-transport">
            <div class="transport-time">
              <span>Current: <strong>{{ formatTime(previewTime) }}</strong></span>
              <span class="transport-dur">Total: {{ formatTime(duration) }}</span>
            </div>

            <!-- Visual timeline bar -->
            <div class="timeline-slider-box">
              <div
                class="timeline-active-slice"
                :style="{
                  left: `${(startTime / duration) * 100}%`,
                  width: `${(cutDuration / duration) * 100}%`
                }"
              ></div>
              <div
                class="timeline-playhead"
                :style="{ left: `${(previewTime / duration) * 100}%` }"
              ></div>
            </div>

            <div class="transport-buttons">
              <button class="btn btn-secondary btn-sm" @click="seekToStart">
                <RotateCcw :size="14" />
                <span>Play Cut</span>
              </button>
              <button class="btn btn-secondary btn-sm" @click="setStartToCurrent">
                <span>Set Start: {{ formatTime(previewTime) }}</span>
              </button>
              <button class="btn btn-secondary btn-sm" @click="setEndToCurrent">
                <span>Set End: {{ formatTime(previewTime) }}</span>
              </button>
            </div>
          </div>
        </div>

        <!-- Trimming Config Form -->
        <form class="trim-config-panel" @submit.prevent="handleTrim">
          <!-- Time range sliders and inputs -->
          <div class="time-range-group">
            <div class="time-row">
              <div class="time-field">
                <label>Start Time (seconds)</label>
                <input
                  v-model.number="startTime"
                  type="number"
                  step="0.1"
                  min="0"
                  :max="endTime - 0.1"
                  class="form-input"
                  required
                />
              </div>

              <div class="time-arrow">
                <ArrowRight :size="18" />
              </div>

              <div class="time-field">
                <label>End Time (seconds)</label>
                <input
                  v-model.number="endTime"
                  type="number"
                  step="0.1"
                  :min="startTime + 0.1"
                  :max="duration"
                  class="form-input"
                  required
                />
              </div>
            </div>

            <div class="cut-duration-pill">
              <Scissors :size="14" />
              <span>Resulting Duration: <strong>{{ cutDuration.toFixed(1) }}s</strong> ({{ formatTime(cutDuration) }})</span>
            </div>
          </div>

          <!-- Metadata -->
          <div class="form-group">
            <label>New Clip Title</label>
            <input
              v-model="trimTitle"
              type="text"
              class="form-input"
              placeholder="Title for trimmed clip"
              required
            />
          </div>

          <div class="form-group">
            <label>Description (optional)</label>
            <textarea
              v-model="trimDescription"
              rows="2"
              class="form-textarea"
              placeholder="What makes this highlight special?"
            ></textarea>
          </div>

          <!-- Visibility & Collection in 2 columns -->
          <div class="form-row-2">
            <div class="form-group">
              <label>Visibility</label>
              <div class="vis-options">
                <label class="vis-pill" :class="{ active: trimVisibility === 'public' }">
                  <input type="radio" v-model="trimVisibility" value="public" class="vis-radio" />
                  <Globe :size="13" />
                  <span>Public</span>
                </label>
                <label class="vis-pill" :class="{ active: trimVisibility === 'unlisted' }">
                  <input type="radio" v-model="trimVisibility" value="unlisted" class="vis-radio" />
                  <Link2 :size="13" />
                  <span>Unlisted</span>
                </label>
                <label class="vis-pill" :class="{ active: trimVisibility === 'private' }">
                  <input type="radio" v-model="trimVisibility" value="private" class="vis-radio" />
                  <Lock :size="13" />
                  <span>Private</span>
                </label>
              </div>
            </div>

            <div class="form-group">
              <label>Add to Collection (optional)</label>
              <select v-model="selectedCollectionId" class="form-select">
                <option value="">None</option>
                <option v-for="col in allCollections" :key="col.id" :value="col.id">
                  {{ col.name }}
                </option>
              </select>
            </div>
          </div>

          <div class="modal-footer">
            <button type="button" class="btn btn-secondary" :disabled="isSubmitting" @click="emit('close')">
              Cancel
            </button>
            <button type="submit" class="btn btn-primary" :disabled="isSubmitting || cutDuration <= 0">
              <Sparkles v-if="!isSubmitting" :size="16" />
              <span>{{ isSubmitting ? 'Cutting Clip...' : 'Cut & Save Clip' }}</span>
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(5, 8, 17, 0.82);
  backdrop-filter: blur(8px);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
  z-index: 1000;
  animation: fadeIn 0.15s ease-out;
}

.trim-dialog {
  max-width: 960px;
  width: 100%;
  background: $bg-surface;
  border: 1px solid $border-medium;
  border-radius: $radius-lg;
  box-shadow: 0 20px 50px rgba(0, 0, 0, 0.6);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid $border-subtle;

  .modal-title-wrap {
    display: flex;
    align-items: center;
    gap: 0.6rem;
  }

  .modal-icon {
    color: $accent-cyan;
  }

  .modal-title {
    font-size: 1.25rem;
    font-weight: 700;
    color: $text-primary;
    margin: 0;
  }

  .btn-close {
    background: transparent;
    border: none;
    color: $text-muted;
    cursor: pointer;
    padding: 0.35rem;
    border-radius: $radius-sm;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all $transition-fast;

    &:hover {
      color: $text-primary;
      background: rgba(255, 255, 255, 0.08);
    }
  }
}

.trim-body {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.5rem;
  padding: 1.5rem;

  @media (max-width: 860px) {
    grid-template-columns: 1fr;
    max-height: 80vh;
    overflow-y: auto;
  }
}

.preview-panel {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.video-preview-wrapper {
  position: relative;
  width: 100%;
  aspect-ratio: 16 / 9;
  background: #000;
  border-radius: $radius-md;
  overflow: hidden;
  border: 1px solid $border-subtle;

  .trim-preview-video {
    width: 100%;
    height: 100%;
    object-fit: contain;
    cursor: pointer;
  }

  .play-overlay-btn {
    position: absolute;
    inset: 0;
    margin: auto;
    width: 56px;
    height: 56px;
    border-radius: 50%;
    background: rgba(0, 242, 254, 0.8);
    border: none;
    color: #050811;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    opacity: 0;
    transition: opacity 0.2s, transform $transition-fast;

    .play-offset {
      margin-left: 3px;
    }

    &:hover {
      transform: scale(1.08);
    }
  }

  &:hover .play-overlay-btn {
    opacity: 1;
  }
}

.preview-transport {
  display: flex;
  flex-direction: column;
  gap: 0.6rem;

  .transport-time {
    display: flex;
    justify-content: space-between;
    font-size: 0.82rem;
    color: $text-secondary;

    strong {
      color: $accent-cyan;
    }

    .transport-dur {
      color: $text-muted;
    }
  }

  .timeline-slider-box {
    position: relative;
    width: 100%;
    height: 10px;
    background: rgba(255, 255, 255, 0.1);
    border-radius: 5px;
    overflow: hidden;

    .timeline-active-slice {
      position: absolute;
      top: 0;
      height: 100%;
      background: rgba(0, 242, 254, 0.45);
      border-left: 2px solid $accent-cyan;
      border-right: 2px solid $accent-cyan;
    }

    .timeline-playhead {
      position: absolute;
      top: 0;
      width: 3px;
      height: 100%;
      background: #fff;
      box-shadow: 0 0 6px rgba(255, 255, 255, 0.8);
    }
  }

  .transport-buttons {
    display: flex;
    gap: 0.4rem;
    flex-wrap: wrap;

    .btn-sm {
      padding: 0.35rem 0.65rem;
      font-size: 0.78rem;
    }
  }
}

.trim-config-panel {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.time-range-group {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-md;
  padding: 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;

  .time-row {
    display: flex;
    align-items: center;
    gap: 0.6rem;

    .time-field {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 0.3rem;

      label {
        font-size: 0.75rem;
        font-weight: 600;
        color: $text-secondary;
      }
    }

    .time-arrow {
      color: $text-muted;
      margin-top: 1.1rem;
    }
  }

  .cut-duration-pill {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    font-size: 0.8rem;
    color: $accent-cyan;
    background: rgba(0, 242, 254, 0.08);
    padding: 0.4rem 0.6rem;
    border-radius: $radius-sm;
    border: 1px solid rgba(0, 242, 254, 0.2);
  }
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;

  label {
    font-size: 0.8rem;
    font-weight: 600;
    color: $text-secondary;
  }
}

.form-row-2 {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.75rem;
}

.vis-options {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 0.35rem;
}

.vis-pill {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.3rem;
  padding: 0.4rem 0.5rem;
  border-radius: $radius-sm;
  border: 1px solid $border-medium;
  background: $bg-input;
  color: $text-secondary;
  font-size: 0.75rem;
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
    background: rgba(0, 242, 254, 0.12);
    border-color: $accent-cyan;
    color: $accent-cyan;
  }
}

.modal-footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.6rem;
  margin-top: auto;
  padding-top: 0.75rem;
  border-top: 1px solid $border-subtle;
}
</style>
