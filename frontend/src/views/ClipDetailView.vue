<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { api, type Clip, type Collection } from '../api/client';
import { useAuth } from '../stores/auth';
import { useToast } from '../stores/toast';
import VideoPlayer from '../components/VideoPlayer.vue';
import {
  Share2,
  Copy,
  Check,
  Download,
  Folder,
  Plus,
  Trash2,
  Edit3,
  Eye,
  Clock,
  HardDrive,
  FileVideo,
  ArrowLeft,
  Lock,
  Link2,
  Globe,
  Scissors,
  Film,
  ArrowUpRight,
  Sparkles,
  Tag,
  Heart
} from '@lucide/vue';
import TrimModal from '../components/TrimModal.vue';
import AiAnalysisModal from '../components/AiAnalysisModal.vue';
import type { AiSubClipSuggestion } from '../api/client';
import { useLikes } from '../composables/useLikes';

const route = useRoute();
const router = useRouter();
const { state: auth } = useAuth();
const { success, error: toastError } = useToast();
const { isLiked, toggleLike } = useLikes();

const playerRef = ref<InstanceType<typeof VideoPlayer> | null>(null);
const clip = ref<Clip | null>(null);
const isLoading = ref(true);
const isEditing = ref(false);
const isTrimming = ref(false);
const isAiAnalyzing = ref(false);
const trimInitialStartTime = ref<number | undefined>(undefined);
const trimInitialEndTime = ref<number | undefined>(undefined);
const trimInitialTitle = ref<string | undefined>(undefined);
const trimInitialDescription = ref<string | undefined>(undefined);

const editTitle = ref('');
const editDescription = ref('');
const editVisibility = ref<'public' | 'unlisted' | 'private'>('public');
const editTagsInput = ref('');
const allCollections = ref<Collection[]>([]);
const selectedNewColId = ref('');
const isCopying = ref(false);

const clipId = computed(() => route.params.id as string);

function onClipTrimmed(newClip: Clip) {
  router.push(`/clip/${newClip.id}`);
}

function openTrimWithSuggestion(suggestion: AiSubClipSuggestion) {
  trimInitialStartTime.value = suggestion.startSeconds;
  trimInitialEndTime.value = suggestion.endSeconds;
  trimInitialTitle.value = suggestion.title;
  trimInitialDescription.value = suggestion.description;
  isTrimming.value = true;
}

function openNormalTrim() {
  trimInitialStartTime.value = undefined;
  trimInitialEndTime.value = undefined;
  trimInitialTitle.value = undefined;
  trimInitialDescription.value = undefined;
  isTrimming.value = true;
}

function onTagsUpdated(newTags: string[]) {
  if (clip.value) {
    clip.value.tags = newTags;
    editTagsInput.value = newTags.join(', ');
  }
}

function onAnalysisUpdated(analysis: any) {
  if (clip.value) {
    clip.value.latestAiAnalysis = analysis;
  }
}

async function loadClip() {
  isLoading.value = true;
  try {
    clip.value = await api.getClip(clipId.value);
    editTitle.value = clip.value.title;
    editDescription.value = clip.value.description || '';
    editVisibility.value = clip.value.visibility || 'public';
    editTagsInput.value = (clip.value.tags || []).join(', ');
  } catch (err: any) {
    console.error('Failed to load clip', err);
    toastError('Clip not found');
  } finally {
    isLoading.value = false;
  }
}

async function loadCollections() {
  if (!auth.isAuthenticated) return;
  try {
    allCollections.value = await api.getCollections();
  } catch (err) {
    console.error(err);
  }
}

async function copyShareLink() {
  if (!clip.value) return;
  const url = `${window.location.origin}/clip/${clip.value.id}`;
  try {
    await navigator.clipboard.writeText(url);
    isCopying.value = true;
    success('Public share link copied to clipboard!');
    setTimeout(() => {
      isCopying.value = false;
    }, 2000);
  } catch (err) {
    console.error(err);
  }
}

async function saveEdit() {
  if (!clip.value || !editTitle.value.trim()) return;
  try {
    const parsedTags = editTagsInput.value
      .split(',')
      .map(t => t.trim().replace(/^#+/, ''))
      .filter(t => t.length > 0);

    const updated = await api.updateClip(clip.value.id, {
      title: editTitle.value.trim(),
      description: editDescription.value.trim() || null,
      visibility: editVisibility.value,
      tags: parsedTags,
    });
    clip.value = updated;
    isEditing.value = false;
    success('Clip details updated!');
  } catch (err: any) {
    toastError(err.message || 'Failed to update clip');
  }
}

async function deleteClip() {
  if (!clip.value) return;
  if (!confirm(`Are you sure you want to delete "${clip.value.title}"? This cannot be undone.`)) {
    return;
  }

  try {
    await api.deleteClip(clip.value.id);
    success('Clip deleted successfully');
    router.push('/');
  } catch (err: any) {
    toastError(err.message || 'Failed to delete clip');
  }
}

async function addToCollection() {
  if (!clip.value || !selectedNewColId.value) return;
  try {
    await api.addClipToCollection(selectedNewColId.value, clip.value.id);
    success('Added clip to collection');
    selectedNewColId.value = '';
    await loadClip();
  } catch (err: any) {
    toastError(err.message || 'Failed to add to collection');
  }
}

async function removeFromCollection(collectionId: string) {
  if (!clip.value) return;
  try {
    await api.removeClipFromCollection(collectionId, clip.value.id);
    success('Removed from collection');
    await loadClip();
  } catch (err: any) {
    toastError(err.message || 'Failed to remove from collection');
  }
}

function formatBytes(bytes: number): string {
  if (bytes === 0) return '0 B';
  const k = 1024;
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${(bytes / Math.pow(k, i)).toFixed(1)} ${sizes[i]}`;
}

function formatDuration(seconds?: number | null): string {
  if (!seconds || isNaN(seconds)) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function formatDate(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleString(undefined, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return dateStr;
  }
}

onMounted(() => {
  loadClip();
  loadCollections();
});

watch(
  () => route.params.id,
  () => {
    loadClip();
  }
);
</script>

<template>
  <div class="clip-detail-view">
    <div class="top-back-bar">
      <router-link to="/" class="back-link">
        <ArrowLeft :size="16" />
        <span>Back to Clips</span>
      </router-link>
    </div>

    <div v-if="isLoading" class="loading-box">
      <div class="loader-spinner"></div>
      <p>Loading clip...</p>
    </div>

    <div v-else-if="clip" class="clip-content-layout">
      <!-- Player column -->
      <div class="player-column">
        <VideoPlayer
          ref="playerRef"
          :src="clip.videoUrl"
          :poster="clip.thumbnailUrl || undefined"
          :autoplay="true"
        />

        <!-- Title & Metadata section -->
        <div class="clip-header-card">
          <template v-if="!isEditing">
            <div class="title-and-actions">
              <div class="title-with-badge">
                <h1 class="clip-main-title">{{ clip.title }}</h1>
                <span
                  class="badge"
                  :class="clip.visibility === 'public' ? 'badge-cyan' : clip.visibility === 'unlisted' ? 'badge-amber' : 'badge-rose'"
                >
                  <Globe v-if="clip.visibility === 'public'" :size="12" />
                  <Link2 v-else-if="clip.visibility === 'unlisted'" :size="12" />
                  <Lock v-else :size="12" />
                  <span>{{ clip.visibility }}</span>
                </span>
              </div>
              <div v-if="auth.isAuthenticated" class="admin-quick-actions">
                <button class="btn btn-primary btn-sm btn-ai-action" @click="isAiAnalyzing = true">
                  <Sparkles :size="14" />
                  <span>AI Analyse</span>
                </button>
                <button class="btn btn-secondary btn-sm btn-trim" @click="openNormalTrim">
                  <Scissors :size="14" />
                  <span>Cut Clip</span>
                </button>
                <button class="btn btn-secondary btn-sm" @click="isEditing = true">
                  <Edit3 :size="14" />
                  <span>Edit</span>
                </button>
                <button class="btn btn-danger btn-sm" @click="deleteClip">
                  <Trash2 :size="14" />
                  <span>Delete</span>
                </button>
              </div>
            </div>

            <p v-if="clip.description" class="clip-description">
              {{ clip.description }}
            </p>

            <!-- Tags List -->
            <div v-if="clip.tags && clip.tags.length > 0" class="clip-tags-row">
              <router-link
                v-for="t in clip.tags"
                :key="t"
                :to="`/?tag=${encodeURIComponent(t)}`"
                class="clip-tag-badge"
              >
                <span>#{{ t }}</span>
              </router-link>
            </div>
          </template>

          <template v-else>
            <!-- Edit Form -->
            <form class="edit-form" @submit.prevent="saveEdit">
              <div class="form-group">
                <label>Title</label>
                <input v-model="editTitle" type="text" class="form-input" required />
              </div>
              <div class="form-group">
                <label>Description</label>
                <textarea v-model="editDescription" rows="3" class="form-textarea"></textarea>
              </div>
              <div class="form-group">
                <label>Tags (comma separated)</label>
                <input
                  v-model="editTagsInput"
                  type="text"
                  class="form-input"
                  placeholder="e.g. apex, clutch, funny, sniper"
                />
              </div>
              <div class="form-group">
                <label>Visibility</label>
                <div class="vis-edit-options">
                  <label class="vis-pill-label" :class="{ active: editVisibility === 'public' }">
                    <input type="radio" v-model="editVisibility" value="public" class="vis-radio" />
                    <Globe :size="13" />
                    <span>Public</span>
                  </label>
                  <label class="vis-pill-label" :class="{ active: editVisibility === 'unlisted' }">
                    <input type="radio" v-model="editVisibility" value="unlisted" class="vis-radio" />
                    <Link2 :size="13" />
                    <span>Unlisted</span>
                  </label>
                  <label class="vis-pill-label" :class="{ active: editVisibility === 'private' }">
                    <input type="radio" v-model="editVisibility" value="private" class="vis-radio" />
                    <Lock :size="13" />
                    <span>Private</span>
                  </label>
                </div>
              </div>
              <div class="edit-actions">
                <button type="button" class="btn btn-secondary btn-sm" @click="isEditing = false">
                  Cancel
                </button>
                <button type="submit" class="btn btn-primary btn-sm">
                  Save Changes
                </button>
              </div>
            </form>
          </template>

          <!-- Metrics bar -->
          <div class="metrics-bar">
            <button
              class="like-btn"
              :class="{ 'is-liked': isLiked(clip.id) }"
              :title="isLiked(clip.id) ? 'Unlike this clip' : 'Like this clip'"
              @click="toggleLike(clip)"
            >
              <Heart :size="15" class="like-icon" :class="{ 'fill-current': isLiked(clip.id) }" />
              <span><strong>{{ clip.likesCount }}</strong> {{ clip.likesCount === 1 ? 'like' : 'likes' }}</span>
            </button>
            <div class="metric-dot">•</div>
            <div class="metric-item">
              <Eye :size="15" />
              <span><strong>{{ clip.viewsCount }}</strong> views</span>
            </div>
            <div class="metric-dot">•</div>
            <div class="metric-item">
              <Clock :size="15" />
              <span>{{ formatDate(clip.createdAtUtc) }}</span>
            </div>
            <div class="metric-dot">•</div>
            <div class="metric-item">
              <HardDrive :size="15" />
              <span>{{ formatBytes(clip.fileSizeBytes) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Sidebar / Share Column -->
      <div class="sidebar-column">
        <!-- Public Share Card -->
        <div class="side-card share-card">
          <h3>
            <Share2 :size="16" class="card-icon" />
            <span>Share Link</span>
          </h3>

          <div v-if="clip.visibility === 'private'" class="visibility-notice private-notice">
            <Lock :size="15" />
            <span>This clip is <strong>Private</strong>. Only you (the admin) can access or view it. Public links will return Not Found.</span>
          </div>
          <div v-else-if="clip.visibility === 'unlisted'" class="visibility-notice unlisted-notice">
            <Link2 :size="15" />
            <span>This clip is <strong>Unlisted</strong>. It is hidden from browse lists, but anyone with this direct link can view it.</span>
          </div>
          <p v-else class="share-hint">Anyone with this link can watch or embed this clip:</p>

          <div class="share-input-group">
            <input
              type="text"
              readonly
              :value="`${$route.path ? windowOrigin : ''}/clip/${clip.id}`"
              class="form-input share-input"
              @click="copyShareLink"
            />
            <button class="btn btn-primary btn-copy" @click="copyShareLink">
              <Check v-if="isCopying" :size="16" />
              <Copy v-else :size="16" />
              <span>{{ isCopying ? 'Copied!' : 'Copy' }}</span>
            </button>
          </div>

          <a :href="`${clip.videoUrl}?download=1`" download class="btn btn-secondary btn-download">
            <Download :size="15" />
            <span>Download Original Video</span>
          </a>
        </div>

        <!-- Collections Card -->
        <div class="side-card collections-card">
          <h3>
            <Folder :size="16" class="card-icon" />
            <span>Collections</span>
          </h3>

          <div v-if="clip.collections && clip.collections.length > 0" class="col-list">
            <div v-for="col in clip.collections" :key="col.id" class="col-item">
              <router-link :to="`/collection/${col.id}`" class="col-link">
                <Folder :size="14" />
                <span>{{ col.name }}</span>
              </router-link>
              <button
                v-if="auth.isAuthenticated"
                class="remove-col-btn"
                title="Remove from this collection"
                @click="removeFromCollection(col.id)"
              >
                &times;
              </button>
            </div>
          </div>
          <p v-else class="empty-col-text">This clip is not in any collection yet.</p>

          <!-- Add to collection (Admin only) -->
          <div v-if="auth.isAuthenticated" class="add-col-section">
            <select v-model="selectedNewColId" class="form-select form-select-sm">
              <option value="">Select collection...</option>
              <option
                v-for="col in allCollections.filter(c => !clip?.collections.some(cc => cc.id === c.id))"
                :key="col.id"
                :value="col.id"
              >
                {{ col.name }}
              </option>
            </select>
            <button
              class="btn btn-secondary btn-sm"
              :disabled="!selectedNewColId"
              @click="addToCollection"
            >
              <Plus :size="14" />
              <span>Add</span>
            </button>
          </div>
        </div>

        <!-- Source Clip (Parent) if this clip was cut from another -->
        <div v-if="clip.parentClip" class="side-card lineage-card parent-clip-card">
          <h3>
            <Film :size="16" class="card-icon" />
            <span>Cut From (Source Clip)</span>
          </h3>
          <router-link :to="`/clip/${clip.parentClip.id}`" class="lineage-clip-item">
            <div class="lineage-thumb-wrap">
              <img :src="clip.parentClip.thumbnailUrl || ''" :alt="clip.parentClip.title" class="lineage-thumb" />
              <span v-if="clip.parentClip.durationSeconds" class="lineage-dur-badge">
                {{ formatDuration(clip.parentClip.durationSeconds) }}
              </span>
            </div>
            <div class="lineage-meta">
              <span class="lineage-title">{{ clip.parentClip.title }}</span>
              <span class="lineage-sub">
                <span class="badge badge-subtle">{{ clip.parentClip.visibility }}</span>
                <span class="lineage-link-hint">
                  <span>View Original</span>
                  <ArrowUpRight :size="12" />
                </span>
              </span>
            </div>
          </router-link>
        </div>

        <!-- Cut Clips (Children) if any clips were cut from this clip -->
        <div v-if="clip.childClips && clip.childClips.length > 0" class="side-card lineage-card child-clips-card">
          <h3>
            <Scissors :size="16" class="card-icon" />
            <span>Cut Clips ({{ clip.childClips.length }})</span>
          </h3>
          <div class="child-clips-list">
            <router-link
              v-for="child in clip.childClips"
              :key="child.id"
              :to="`/clip/${child.id}`"
              class="lineage-clip-item"
            >
              <div class="lineage-thumb-wrap">
                <img :src="child.thumbnailUrl || ''" :alt="child.title" class="lineage-thumb" />
                <span v-if="child.durationSeconds" class="lineage-dur-badge">
                  {{ formatDuration(child.durationSeconds) }}
                </span>
              </div>
              <div class="lineage-meta">
                <span class="lineage-title">{{ child.title }}</span>
                <span class="lineage-sub">
                  <span class="badge badge-subtle">{{ child.visibility }}</span>
                  <span class="lineage-date">{{ formatDate(child.createdAtUtc) }}</span>
                </span>
              </div>
            </router-link>
          </div>
        </div>

        <!-- Technical specs -->
        <div class="side-card specs-card">
          <h3>
            <FileVideo :size="16" class="card-icon" />
            <span>File Details</span>
          </h3>
          <ul class="specs-list">
            <li>
              <span class="label">Original Name</span>
              <span class="value text-truncate" :title="clip.originalFileName">{{ clip.originalFileName }}</span>
            </li>
            <li>
              <span class="label">Format</span>
              <span class="value">{{ clip.contentType }}</span>
            </li>
            <li>
              <span class="label">Public ID</span>
              <span class="value"><code>{{ clip.id }}</code></span>
            </li>
          </ul>
        </div>
      </div>
    </div>

    <!-- Error state -->
    <div v-else class="not-found-state">
      <h2>Clip Not Found</h2>
      <p>This clip may have been removed or the link is incorrect.</p>
      <router-link to="/" class="btn btn-primary">Return Home</router-link>
    </div>

    <!-- Trim Modal (Admin only) -->
    <TrimModal
      v-if="clip && auth.isAuthenticated"
      :show="isTrimming"
      :source-clip="clip"
      :all-collections="allCollections"
      :initial-current-time="playerRef?.currentTime ?? 0"
      :initial-start-time="trimInitialStartTime"
      :initial-end-time="trimInitialEndTime"
      :initial-title="trimInitialTitle"
      :initial-description="trimInitialDescription"
      @close="isTrimming = false"
      @trimmed="onClipTrimmed"
    />

    <!-- AI Analysis Modal (Admin only) -->
    <AiAnalysisModal
      v-if="clip && auth.isAuthenticated"
      :show="isAiAnalyzing"
      :clip="clip"
      @close="isAiAnalyzing = false"
      @tags-updated="onTagsUpdated"
      @analysis-updated="onAnalysisUpdated"
      @open-trim-with-suggestion="openTrimWithSuggestion"
    />
  </div>
</template>

<script lang="ts">
export default {
  computed: {
    windowOrigin() {
      return typeof window !== 'undefined' ? window.location.origin : '';
    }
  }
}
</script>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.clip-detail-view {
  max-width: 1400px;
  margin: 0 auto;
  padding: 1.25rem 1.5rem 3rem;
  width: 100%;
}

.top-back-bar {
  margin-bottom: 1rem;

  .back-link {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    color: $text-secondary;
    font-size: 0.85rem;
    font-weight: 500;
    text-decoration: none;
    transition: color $transition-fast;

    &:hover {
      color: $accent-cyan;
    }
  }
}

.clip-content-layout {
  display: grid;
  grid-template-columns: 1fr 360px;
  gap: 1.75rem;
  align-items: start;

  @media (max-width: 1024px) {
    grid-template-columns: 1fr;
  }
}

.player-column {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.clip-header-card {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-lg;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;

  .title-and-actions {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 1rem;

    .title-with-badge {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0.65rem;
    }

    .clip-main-title {
      font-size: 1.5rem;
      font-weight: 800;
      letter-spacing: -0.02em;
      line-height: 1.3;
      color: $text-primary;
    }

    .admin-quick-actions {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      flex-shrink: 0;

      .btn {
        white-space: nowrap;
        flex-shrink: 0;
      }
    }
  }

  .vis-edit-options {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 0.5rem;
  }

  .vis-pill-label {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.4rem;
    padding: 0.45rem 0.6rem;
    border-radius: $radius-md;
    border: 1px solid $border-medium;
    background: $bg-input;
    color: $text-secondary;
    font-size: 0.8rem;
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

  .clip-description {
    font-size: 0.95rem;
    line-height: 1.6;
    color: $text-secondary;
    white-space: pre-wrap;
  }

  .clip-tags-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
    margin-top: -0.25rem;

    .clip-tag-badge {
      display: inline-flex;
      align-items: center;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid $border-subtle;
      color: $text-secondary;
      border-radius: $radius-xs;
      padding: 0.15rem 0.5rem;
      font-size: 0.75rem;
      font-weight: 500;
      text-decoration: none;
      transition: color $transition-fast, border-color $transition-fast;

      &:hover {
        color: #38bdf8;
        border-color: rgba(56, 189, 248, 0.4);
      }
    }
  }

  .btn-ai-action {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    background: #2563eb;
    color: #ffffff;
    font-weight: 500;
    border: 1px solid #2563eb;

    &:hover {
      background: #1d4ed8;
      border-color: #1d4ed8;
    }
  }

  .metrics-bar {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    font-size: 0.82rem;
    color: $text-muted;
    border-top: 1px solid $border-subtle;
    padding-top: 0.85rem;
    flex-wrap: wrap;

    .like-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      background: rgba(255, 255, 255, 0.05);
      border: 1px solid $border-subtle;
      color: $text-secondary;
      padding: 0.25rem 0.65rem;
      border-radius: $radius-sm;
      font-size: 0.8rem;
      cursor: pointer;
      transition: all $transition-fast;

      strong {
        color: $text-primary;
      }

      &:hover {
        background: rgba(244, 63, 94, 0.1);
        border-color: rgba(244, 63, 94, 0.3);
        color: #f43f5e;

        strong {
          color: #f43f5e;
        }
      }

      &.is-liked {
        background: rgba(244, 63, 94, 0.15);
        border-color: rgba(244, 63, 94, 0.5);
        color: #f43f5e;

        strong {
          color: #f43f5e;
        }

        .like-icon {
          fill: #f43f5e;
        }
      }
    }

    .metric-item {
      display: flex;
      align-items: center;
      gap: 0.35rem;

      strong {
        color: $text-primary;
      }
    }

    .metric-dot {
      color: rgba(255, 255, 255, 0.15);
    }
  }
}

.edit-form {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;

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

  .edit-actions {
    display: flex;
    justify-content: flex-end;
    gap: 0.5rem;
  }
}

.sidebar-column {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.side-card {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-lg;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.85rem;

  h3 {
    font-size: 0.95rem;
    font-weight: 700;
    display: flex;
    align-items: center;
    gap: 0.45rem;
    color: $text-primary;

    .card-icon {
      color: $accent-cyan;
    }
  }
}

.share-card {
  .visibility-notice {
    display: flex;
    align-items: flex-start;
    gap: 0.5rem;
    padding: 0.6rem 0.75rem;
    border-radius: $radius-md;
    font-size: 0.78rem;
    line-height: 1.4;
    margin-bottom: 0.35rem;

    &.private-notice {
      background: rgba($accent-rose, 0.12);
      border: 1px solid rgba($accent-rose, 0.3);
      color: #fda4af;
    }

    &.unlisted-notice {
      background: rgba($accent-amber, 0.12);
      border: 1px solid rgba($accent-amber, 0.3);
      color: #fcd34d;
    }
  }

  .share-hint {
    font-size: 0.8rem;
    color: $text-secondary;
  }

  .share-input-group {
    display: flex;
    gap: 0.4rem;

    .share-input {
      font-size: 0.8rem;
      background: $bg-root;
      cursor: pointer;
    }

    .btn-copy {
      padding: 0.45rem 0.85rem;
      white-space: nowrap;
    }
  }

  .btn-download {
    width: 100%;
    margin-top: 0.25rem;
  }
}

.collections-card {
  .col-list {
    display: flex;
    flex-direction: column;
    gap: 0.4rem;

    .col-item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: $bg-surface;
      padding: 0.45rem 0.75rem;
      border-radius: $radius-sm;
      border: 1px solid $border-subtle;

      .col-link {
        display: flex;
        align-items: center;
        gap: 0.4rem;
        color: #c4b5fd;
        font-size: 0.85rem;
        text-decoration: none;
        font-weight: 500;

        &:hover {
          color: #fff;
        }
      }

      .remove-col-btn {
        background: transparent;
        border: none;
        color: $text-muted;
        cursor: pointer;
        font-size: 1.1rem;
        line-height: 1;

        &:hover {
          color: $accent-rose;
        }
      }
    }
  }

  .empty-col-text {
    font-size: 0.8rem;
    color: $text-muted;
  }

  .add-col-section {
    display: flex;
    gap: 0.4rem;
    margin-top: 0.5rem;
    border-top: 1px solid $border-subtle;
    padding-top: 0.75rem;

    .form-select-sm {
      padding: 0.35rem 0.6rem;
      font-size: 0.8rem;
    }
  }
}

.lineage-card {
  .child-clips-list {
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .lineage-clip-item {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    background: $bg-surface;
    border: 1px solid $border-subtle;
    border-radius: $radius-md;
    padding: 0.5rem;
    text-decoration: none;
    transition: all $transition-fast;

    &:hover {
      background: $bg-surface-elevated;
      border-color: rgba(0, 242, 254, 0.4);
      transform: translateX(2px);

      .lineage-title {
        color: $accent-cyan;
      }
    }

    .lineage-thumb-wrap {
      position: relative;
      width: 72px;
      aspect-ratio: 16 / 9;
      background: #000;
      border-radius: 4px;
      overflow: hidden;
      flex-shrink: 0;

      .lineage-thumb {
        width: 100%;
        height: 100%;
        object-fit: cover;
      }

      .lineage-dur-badge {
        position: absolute;
        bottom: 2px;
        right: 3px;
        background: rgba(0, 0, 0, 0.8);
        color: #fff;
        font-size: 0.65rem;
        font-weight: 700;
        padding: 0.05rem 0.25rem;
        border-radius: 2px;
      }
    }

    .lineage-meta {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      min-width: 0;
      flex: 1;

      .lineage-title {
        font-size: 0.82rem;
        font-weight: 600;
        color: $text-primary;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        transition: color $transition-fast;
      }

      .lineage-sub {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 0.75rem;
        color: $text-muted;

        .badge-subtle {
          font-size: 0.65rem;
          padding: 0.1rem 0.35rem;
          background: rgba(255, 255, 255, 0.06);
          color: $text-secondary;
          border-radius: 3px;
          text-transform: capitalize;
        }

        .lineage-link-hint {
          display: flex;
          align-items: center;
          gap: 0.2rem;
          color: $accent-cyan;
          font-weight: 500;
        }

        .lineage-date {
          font-size: 0.7rem;
        }
      }
    }
  }
}

.specs-card {
  .specs-list {
    list-style: none;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;

    li {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.8rem;

      .label {
        color: $text-muted;
      }

      .value {
        color: $text-secondary;
        font-weight: 500;

        code {
          background: rgba(255, 255, 255, 0.08);
          padding: 0.1rem 0.35rem;
          border-radius: 4px;
          color: $accent-cyan;
        }
      }

      .text-truncate {
        max-width: 180px;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }
    }
  }
}

.btn-sm {
  padding: 0.35rem 0.7rem;
  font-size: 0.78rem;
}

.loading-box {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 6rem 1rem;
  gap: 1rem;
  color: $text-muted;

  .loader-spinner {
    width: 36px;
    height: 36px;
    border: 3px solid rgba(0, 242, 254, 0.15);
    border-top-color: $accent-cyan;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
  }
}

.not-found-state {
  text-align: center;
  padding: 5rem 1rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}
</style>
