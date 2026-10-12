<script setup lang="ts">
import { ref, watch, onMounted, onBeforeUnmount } from 'vue';
import { api, type Clip, type AiClipAnalysisResult, type AiSubClipSuggestion } from '../api/client';
import { useToast } from '../stores/toast';
import {
  Sparkles,
  Tag,
  Scissors,
  Check,
  X,
  Plus,
  Play,
  RotateCcw,
  Loader2,
  AlertCircle,
  Clock,
  Layers,
  ChevronRight,
  Cpu
} from '@lucide/vue';

const props = defineProps<{
  show: boolean;
  clip: Clip;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'tagsUpdated', newTags: string[]): void;
  (e: 'analysisUpdated', analysis: AiClipAnalysisResult): void;
  (e: 'openTrimWithSuggestion', suggestion: AiSubClipSuggestion): void;
}>();

const { success, error: toastError } = useToast();

const selectedMode = ref<'both' | 'tags' | 'subclips'>('both');
const selectedModel = ref<string>('gemini-3.5-flash-lite');
const availableModels = ref<string[]>([
  'gemini-3.5-flash-lite',
  'gemini-3.8-flash',
  'gemini-3.8-pro',
  'gemini-3.1-flash-lite',
  'gemini-2.5-flash',
  'gemini-2.5-flash-lite',
  'gemini-2.5-pro'
]);
const isAnalyzing = ref(false);
const isSavingTags = ref(false);
const analysisResult = ref<AiClipAnalysisResult | null>(null);
const currentClipTags = ref<string[]>([]);
const newTagInput = ref('');
const errorMessage = ref<string | null>(null);
const hasLoadedSavedResult = ref(false);

function formatSeconds(secs: number): string {
  if (isNaN(secs) || secs < 0) return '0:00';
  const m = Math.floor(secs / 60);
  const s = Math.floor(secs % 60);
  return `${m}:${s < 10 ? '0' : ''}${s}`;
}

async function loadAiStatus() {
  try {
    const status = await api.getAiStatus();
    if (status.availableModels && status.availableModels.length > 0) {
      availableModels.value = status.availableModels;
      if (!status.availableModels.includes(selectedModel.value)) {
        selectedModel.value = status.availableModels[0];
      }
    }
  } catch (err) {
    // Keep defaults if status check fails
  }
}

function initData() {
  currentClipTags.value = [...(props.clip.tags || [])];
  errorMessage.value = null;

  // Restore latest saved AI analysis result if available on clip
  if (props.clip.latestAiAnalysis) {
    analysisResult.value = props.clip.latestAiAnalysis;
    hasLoadedSavedResult.value = true;
  } else {
    analysisResult.value = null;
    hasLoadedSavedResult.value = false;
  }
}

watch(
  () => props.show,
  (val) => {
    if (val) {
      initData();
      loadAiStatus();
    }
  },
  { immediate: true }
);

watch(
  () => props.clip,
  () => {
    initData();
  }
);

async function runAnalysis() {
  isAnalyzing.value = true;
  errorMessage.value = null;

  try {
    const res = await api.analyzeClipAi(
      props.clip.id,
      selectedMode.value,
      selectedModel.value,
      props.clip.originalFileName
    );
    analysisResult.value = res;
    hasLoadedSavedResult.value = false;
    emit('analysisUpdated', res);
    success('AI clip analysis complete!');
  } catch (err: any) {
    console.error('AI analysis failed', err);
    const msg = err.message || 'AI analysis failed. Ensure GEMINI_API_KEY is configured.';
    errorMessage.value = msg;
    toastError(msg);
  } finally {
    isAnalyzing.value = false;
  }
}

function addTag(tag: string) {
  const clean = tag.trim().replace(/^#+/, '');
  if (!clean) return;
  if (!currentClipTags.value.some((t) => t.toLowerCase() === clean.toLowerCase())) {
    currentClipTags.value.push(clean);
  }
}

function removeTag(tag: string) {
  currentClipTags.value = currentClipTags.value.filter((t) => t.toLowerCase() !== tag.toLowerCase());
}

function handleAddCustomTag() {
  if (!newTagInput.value.trim()) return;
  addTag(newTagInput.value);
  newTagInput.value = '';
}

function addAllSuggestedTags() {
  if (!analysisResult.value) return;
  for (const t of analysisResult.value.tags) {
    addTag(t);
  }
}

async function saveTagsToClip() {
  isSavingTags.value = true;
  try {
    const updated = await api.updateClip(props.clip.id, {
      title: props.clip.title,
      description: props.clip.description,
      visibility: props.clip.visibility,
      tags: currentClipTags.value,
    });
    emit('tagsUpdated', updated.tags || []);
    success('Tags saved to clip successfully!');
  } catch (err: any) {
    toastError(err.message || 'Failed to save tags');
  } finally {
    isSavingTags.value = false;
  }
}

function cutSubClip(suggestion: AiSubClipSuggestion) {
  emit('openTrimWithSuggestion', suggestion);
  emit('close');
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
    <div class="modal-dialog ai-dialog">
      <div class="modal-header">
        <div class="modal-title-wrap">
          <div class="ai-sparkle-icon">
            <Sparkles :size="20" />
          </div>
          <div>
            <h2 class="modal-title">AI Clip Analysis with Gemini</h2>
            <p class="modal-subtitle">Automatically discover game tags, highlight moments, and sub-clips</p>
          </div>
        </div>
        <button class="btn-close" aria-label="Close" @click="emit('close')">
          <X :size="18" />
        </button>
      </div>

      <div class="modal-body ai-body">
        <!-- Configuration / Option Bar -->
        <div class="analysis-controls-card">
          <div class="controls-top-row">
            <div class="mode-selector-group">
              <span class="mode-label">Analysis Mode:</span>
              <div class="mode-pill-options">
                <label class="mode-pill" :class="{ active: selectedMode === 'both' }">
                  <input type="radio" v-model="selectedMode" value="both" class="mode-radio" />
                  <Layers :size="14" />
                  <span>Both (Tags & Sub-clips)</span>
                </label>
                <label class="mode-pill" :class="{ active: selectedMode === 'tags' }">
                  <input type="radio" v-model="selectedMode" value="tags" class="mode-radio" />
                  <Tag :size="14" />
                  <span>Tags Only</span>
                </label>
                <label class="mode-pill" :class="{ active: selectedMode === 'subclips' }">
                  <input type="radio" v-model="selectedMode" value="subclips" class="mode-radio" />
                  <Scissors :size="14" />
                  <span>Sub-clips Only</span>
                </label>
              </div>
            </div>

            <div class="model-selector-group">
              <span class="mode-label">Model:</span>
              <div class="model-select-wrapper">
                <Cpu :size="14" class="model-select-icon" />
                <select v-model="selectedModel" class="form-select form-select-sm model-dropdown">
                  <option v-for="m in availableModels" :key="m" :value="m">
                    {{ m }}
                  </option>
                </select>
              </div>
            </div>
          </div>

          <div v-if="props.clip.originalFileName" class="controls-context-row">
            <span class="context-label">Filename context:</span>
            <code class="context-filename" :title="props.clip.originalFileName">{{ props.clip.originalFileName }}</code>
          </div>

          <div class="controls-action-row">
            <div v-if="hasLoadedSavedResult" class="saved-analysis-badge">
              <Sparkles :size="13" />
              <span>Loaded previous analysis result. You can re-run to regenerate.</span>
            </div>
            <div v-else class="controls-tip-text">
              Select mode & model, then click analyze to run multimodal inference.
            </div>

            <button
              class="btn btn-primary btn-run-ai"
              :disabled="isAnalyzing"
              @click="runAnalysis"
            >
              <Loader2 v-if="isAnalyzing" :size="16" class="spin-icon" />
              <Sparkles v-else :size="16" />
              <span>{{ isAnalyzing ? 'Analyzing Clip...' : (analysisResult ? 'Re-run Analysis' : 'Analyze Clip') }}</span>
            </button>
          </div>
        </div>

        <!-- Error display -->
        <div v-if="errorMessage" class="error-banner">
          <AlertCircle :size="16" />
          <span>{{ errorMessage }}</span>
        </div>

        <!-- Loading spinner state -->
        <div v-if="isAnalyzing" class="ai-loading-state">
          <div class="ai-orb">
            <Sparkles :size="32" class="sparkle-pulse" />
          </div>
          <h3>Gemini ({{ selectedModel }}) is watching and analyzing your clip...</h3>
          <p>Processing visual action, audio events, and generating suggestions.</p>
        </div>

        <!-- Results section -->
        <div v-else-if="analysisResult" class="results-container">
          <!-- Summary card if provided -->
          <div v-if="analysisResult.summary" class="summary-card">
            <h4>
              <Sparkles :size="14" />
              <span>AI Summary</span>
            </h4>
            <p>{{ analysisResult.summary }}</p>
          </div>

          <!-- Suggested Tags Section -->
          <div v-if="analysisResult.tags && analysisResult.tags.length > 0" class="section-card">
            <div class="section-header">
              <div class="section-title">
                <Tag :size="16" />
                <h3>Suggested Tags ({{ analysisResult.tags.length }})</h3>
              </div>
              <button class="btn btn-secondary btn-xs" @click="addAllSuggestedTags">
                <Plus :size="12" />
                <span>Add All to Clip</span>
              </button>
            </div>

            <div class="tags-cloud">
              <button
                v-for="t in analysisResult.tags"
                :key="t"
                class="tag-pill-suggestion"
                :class="{ added: currentClipTags.includes(t) }"
                @click="addTag(t)"
              >
                <span>#{{ t }}</span>
                <Check v-if="currentClipTags.includes(t)" :size="12" class="tag-check" />
                <Plus v-else :size="12" class="tag-plus" />
              </button>
            </div>
          </div>

          <!-- Suggested Sub-clips Section -->
          <div v-if="analysisResult.subClips && analysisResult.subClips.length > 0" class="section-card">
            <div class="section-header">
              <div class="section-title">
                <Scissors :size="16" />
                <h3>Suggested Sub-clips ({{ analysisResult.subClips.length }})</h3>
              </div>
            </div>

            <div class="subclips-grid">
              <div
                v-for="(sub, idx) in analysisResult.subClips"
                :key="idx"
                class="subclip-card"
              >
                <div class="subclip-header">
                  <div class="subclip-title-wrap">
                    <span class="subclip-index">#{{ idx + 1 }}</span>
                    <h4 class="subclip-title">{{ sub.title }}</h4>
                  </div>
                  <span class="subclip-duration-badge">
                    <Clock :size="12" />
                    <span>{{ formatSeconds(sub.startSeconds) }} - {{ formatSeconds(sub.endSeconds) }} ({{ (sub.endSeconds - sub.startSeconds).toFixed(1) }}s)</span>
                  </span>
                </div>

                <p v-if="sub.description" class="subclip-desc">
                  {{ sub.description }}
                </p>

                <div class="subclip-actions">
                  <button class="btn btn-primary btn-sm btn-cut-now" @click="cutSubClip(sub)">
                    <Scissors :size="14" />
                    <span>Open in Cut Tool</span>
                    <ChevronRight :size="14" />
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Current Tags Management Bar -->
        <div class="clip-tags-manager-card">
          <div class="manager-header">
            <h4>
              <Tag :size="15" />
              <span>Clip Tags</span>
            </h4>
            <span class="tags-hint">Click a tag to remove it, or type to add custom tags</span>
          </div>

          <div class="active-tags-list">
            <span
              v-for="t in currentClipTags"
              :key="t"
              class="active-tag-chip"
            >
              <span>#{{ t }}</span>
              <button class="remove-chip-btn" title="Remove tag" @click="removeTag(t)">
                &times;
              </button>
            </span>

            <span v-if="currentClipTags.length === 0" class="no-tags-text">
              No tags attached yet. Run AI analysis above or add custom tags.
            </span>
          </div>

          <div class="add-custom-tag-row">
            <input
              v-model="newTagInput"
              type="text"
              class="form-input custom-tag-input"
              placeholder="Add tag (e.g. clutch, apex, sniper)..."
              @keydown.enter.prevent="handleAddCustomTag"
            />
            <button class="btn btn-secondary btn-sm" :disabled="!newTagInput.trim()" @click="handleAddCustomTag">
              <Plus :size="14" />
              <span>Add</span>
            </button>
            <button
              class="btn btn-primary btn-sm btn-save-tags"
              :disabled="isSavingTags"
              @click="saveTagsToClip"
            >
              <Check :size="14" />
              <span>{{ isSavingTags ? 'Saving...' : 'Save Tags' }}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.75);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
  z-index: 1000;
  animation: fadeIn 0.12s ease-out;
}

.ai-dialog {
  max-width: 860px;
  width: 100%;
  max-height: 90vh;
  background: $bg-surface;
  border: 1px solid $border-medium;
  border-radius: $radius-lg;
  box-shadow: 0 16px 36px rgba(0, 0, 0, 0.7);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1.15rem 1.35rem;
  border-bottom: 1px solid $border-subtle;
  background: $bg-surface;

  .modal-title-wrap {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .ai-sparkle-icon {
    width: 32px;
    height: 32px;
    border-radius: $radius-sm;
    background: rgba(56, 189, 248, 0.12);
    border: 1px solid rgba(56, 189, 248, 0.25);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #38bdf8;
  }

  .modal-title {
    font-size: 1.2rem;
    font-weight: 800;
    color: $text-primary;
    margin: 0;
    line-height: 1.2;
  }

  .modal-subtitle {
    font-size: 0.8rem;
    color: $text-secondary;
    margin: 0.2rem 0 0;
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

.ai-body {
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
  overflow-y: auto;
}

.analysis-controls-card {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-md;
  padding: 1rem 1.25rem;

  .controls-top-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    flex-wrap: wrap;
  }

  .mode-selector-group {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    flex-wrap: wrap;

    .mode-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: $text-secondary;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .mode-pill-options {
      display: flex;
      gap: 0.4rem;
      flex-wrap: wrap;
    }

    .mode-pill {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.4rem 0.75rem;
      border-radius: 20px;
      font-size: 0.8rem;
      font-weight: 600;
      background: $bg-input;
      border: 1px solid $border-medium;
      color: $text-secondary;
      cursor: pointer;
      transition: all $transition-fast;

      .mode-radio {
        display: none;
      }

      &:hover {
        background: $bg-surface-elevated;
        color: $text-primary;
      }

      &.active {
        background: rgba(0, 242, 254, 0.15);
        border-color: $accent-cyan;
        color: $accent-cyan;
        box-shadow: 0 0 10px rgba(0, 242, 254, 0.2);
      }
    }
  }

  .model-selector-group {
    display: flex;
    align-items: center;
    gap: 0.5rem;

    .mode-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: $text-secondary;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }

    .model-select-wrapper {
      position: relative;
      display: inline-flex;
      align-items: center;

      .model-select-icon {
        position: absolute;
        left: 0.65rem;
        color: $accent-cyan;
        pointer-events: none;
      }

      .model-dropdown {
        padding-left: 2rem;
        padding-right: 2rem;
        height: 32px;
        font-size: 0.8rem;
        font-weight: 600;
        background: $bg-input;
        border-color: $border-medium;
        color: $text-primary;
        border-radius: $radius-sm;

        &:focus {
          border-color: $accent-cyan;
          outline: none;
          box-shadow: 0 0 0 2px rgba(0, 242, 254, 0.2);
        }
      }
    }
  }

  .controls-context-row {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    padding-top: 0.5rem;
    font-size: 0.78rem;

    .context-label {
      color: $text-muted;
      font-weight: 500;
    }

    .context-filename {
      color: $accent-cyan;
      background: rgba(0, 242, 254, 0.08);
      border: 1px solid rgba(0, 242, 254, 0.2);
      padding: 0.15rem 0.45rem;
      border-radius: $radius-sm;
      font-size: 0.75rem;
      max-width: 400px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }

  .controls-action-row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
    flex-wrap: wrap;
    padding-top: 0.75rem;
    border-top: 1px solid rgba(255, 255, 255, 0.06);

    .saved-analysis-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.78rem;
      font-weight: 600;
      color: #93c5fd;
      background: rgba(59, 130, 246, 0.12);
      border: 1px solid rgba(59, 130, 246, 0.25);
      padding: 0.3rem 0.65rem;
      border-radius: 12px;
    }

    .controls-tip-text {
      font-size: 0.8rem;
      color: $text-muted;
    }

    .btn-run-ai {
      padding: 0.55rem 1.25rem;
      font-weight: 700;
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      margin-left: auto;
    }
  }
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  background: rgba($accent-rose, 0.12);
  border: 1px solid rgba($accent-rose, 0.3);
  color: #fda4af;
  padding: 0.75rem 1rem;
  border-radius: $radius-md;
  font-size: 0.85rem;
}

.ai-loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3.5rem 1.5rem;
  text-align: center;
  gap: 0.75rem;

  .ai-orb {
    width: 68px;
    height: 68px;
    border-radius: 50%;
    background: radial-gradient(circle, rgba(0, 242, 254, 0.4) 0%, rgba(121, 40, 202, 0.2) 70%);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #fff;
    box-shadow: 0 0 30px rgba(0, 242, 254, 0.5);
    margin-bottom: 0.5rem;
    animation: pulseOrb 2s infinite ease-in-out;
  }

  .sparkle-pulse {
    animation: spinSlow 6s linear infinite;
  }

  h3 {
    font-size: 1.15rem;
    font-weight: 700;
    color: $text-primary;
  }

  p {
    font-size: 0.85rem;
    color: $text-secondary;
  }
}

@keyframes pulseOrb {
  0%, 100% {
    transform: scale(1);
    box-shadow: 0 0 25px rgba(0, 242, 254, 0.4);
  }
  50% {
    transform: scale(1.08);
    box-shadow: 0 0 45px rgba(0, 242, 254, 0.8);
  }
}

@keyframes spinSlow {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.spin-icon {
  animation: spin 0.8s linear infinite;
}

.results-container {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.summary-card {
  background: rgba(0, 242, 254, 0.04);
  border: 1px solid rgba(0, 242, 254, 0.2);
  border-radius: $radius-md;
  padding: 0.85rem 1.1rem;

  h4 {
    display: flex;
    align-items: center;
    gap: 0.4rem;
    font-size: 0.82rem;
    font-weight: 700;
    color: $accent-cyan;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 0.35rem;
  }

  p {
    font-size: 0.9rem;
    line-height: 1.5;
    color: $text-primary;
  }
}

.section-card {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-md;
  padding: 1.1rem;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;

  .section-header {
    display: flex;
    align-items: center;
    justify-content: space-between;

    .section-title {
      display: flex;
      align-items: center;
      gap: 0.45rem;
      color: $text-primary;

      h3 {
        font-size: 0.95rem;
        font-weight: 700;
      }
    }

    .btn-xs {
      padding: 0.25rem 0.55rem;
      font-size: 0.75rem;
    }
  }
}

.tags-cloud {
  display: flex;
  flex-wrap: wrap;
  gap: 0.45rem;

  .tag-pill-suggestion {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    background: $bg-input;
    border: 1px solid $border-medium;
    border-radius: 16px;
    padding: 0.35rem 0.75rem;
    color: $text-secondary;
    font-size: 0.82rem;
    font-weight: 600;
    cursor: pointer;
    transition: all $transition-fast;

    &:hover {
      background: $bg-surface-elevated;
      color: $text-primary;
      border-color: rgba(255, 255, 255, 0.3);
    }

    &.added {
      background: rgba(16, 185, 129, 0.15);
      border-color: $accent-emerald;
      color: #6ee7b7;
    }

    .tag-check {
      color: $accent-emerald;
    }
  }
}

.subclips-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: 0.85rem;
}

.subclip-card {
  background: $bg-surface;
  border: 1px solid $border-subtle;
  border-radius: $radius-md;
  padding: 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.6rem;
  transition: border-color $transition-fast;

  &:hover {
    border-color: rgba(0, 242, 254, 0.4);
  }

  .subclip-header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 0.5rem;

    .subclip-title-wrap {
      display: flex;
      align-items: center;
      gap: 0.4rem;
    }

    .subclip-index {
      font-size: 0.75rem;
      font-weight: 800;
      color: $accent-cyan;
      background: rgba(0, 242, 254, 0.12);
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
    }

    .subclip-title {
      font-size: 0.9rem;
      font-weight: 700;
      color: $text-primary;
      margin: 0;
    }

    .subclip-duration-badge {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      font-size: 0.72rem;
      font-weight: 600;
      color: #c4b5fd;
      background: rgba(139, 92, 246, 0.15);
      padding: 0.2rem 0.5rem;
      border-radius: 12px;
      white-space: nowrap;
    }
  }

  .subclip-desc {
    font-size: 0.8rem;
    color: $text-secondary;
    line-height: 1.4;
    margin: 0;
  }

  .subclip-actions {
    display: flex;
    justify-content: flex-end;
    margin-top: 0.25rem;

    .btn-cut-now {
      padding: 0.35rem 0.75rem;
      font-size: 0.78rem;
      font-weight: 600;
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
  }
}

.clip-tags-manager-card {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-md;
  padding: 1.1rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;

  .manager-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 0.5rem;

    h4 {
      display: flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.88rem;
      font-weight: 700;
      color: $text-primary;
    }

    .tags-hint {
      font-size: 0.75rem;
      color: $text-muted;
    }
  }

  .active-tags-list {
    display: flex;
    flex-wrap: wrap;
    gap: 0.4rem;
    min-height: 38px;
    padding: 0.5rem;
    background: $bg-root;
    border: 1px solid $border-subtle;
    border-radius: $radius-sm;
    align-items: center;

    .active-tag-chip {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      background: rgba(0, 242, 254, 0.12);
      border: 1px solid rgba(0, 242, 254, 0.3);
      color: $accent-cyan;
      border-radius: 14px;
      padding: 0.2rem 0.55rem;
      font-size: 0.78rem;
      font-weight: 600;

      .remove-chip-btn {
        background: transparent;
        border: none;
        color: rgba(255, 255, 255, 0.5);
        cursor: pointer;
        font-size: 0.9rem;
        line-height: 1;
        padding: 0;

        &:hover {
          color: #fff;
        }
      }
    }

    .no-tags-text {
      font-size: 0.78rem;
      color: $text-muted;
      font-style: italic;
    }
  }

  .add-custom-tag-row {
    display: flex;
    gap: 0.5rem;

    .custom-tag-input {
      flex: 1;
      font-size: 0.82rem;
      padding: 0.4rem 0.75rem;
    }

    .btn-save-tags {
      margin-left: auto;
    }
  }
}
</style>
