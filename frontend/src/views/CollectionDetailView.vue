<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { api, type Collection, type Clip } from '../api/client';
import { useAuth } from '../stores/auth';
import { useToast } from '../stores/toast';
import VideoPlayer from '../components/VideoPlayer.vue';
import CollectionModal from '../components/CollectionModal.vue';
import {
  Folder,
  Share2,
  Copy,
  Check,
  Play,
  Film,
  Edit3,
  Trash2,
  ArrowLeft,
  ChevronUp,
  ChevronDown,
  Clock,
  Eye,
  SkipForward
} from '@lucide/vue';

const route = useRoute();
const router = useRouter();
const { state: auth } = useAuth();
const { success, error: toastError } = useToast();

const collection = ref<Collection | null>(null);
const isLoading = ref(true);
const activeClipIndex = ref(0);
const isCopying = ref(false);
const isEditModalOpen = ref(false);

const collectionId = computed(() => route.params.id as string);

const clips = computed<Clip[]>(() => collection.value?.clips || []);
const activeClip = computed<Clip | null>(() => {
  if (clips.value.length === 0) return null;
  return clips.value[activeClipIndex.value] || clips.value[0];
});

async function loadCollection() {
  isLoading.value = true;
  try {
    collection.value = await api.getCollection(collectionId.value);
  } catch (err: any) {
    console.error(err);
    toastError('Collection not found');
  } finally {
    isLoading.value = false;
  }
}

function selectClip(index: number) {
  activeClipIndex.value = index;
}

function onClipEnded() {
  if (activeClipIndex.value < clips.value.length - 1) {
    activeClipIndex.value++;
    success(`Now playing next clip: ${clips.value[activeClipIndex.value]?.title}`);
  }
}

async function copyShareLink() {
  if (!collection.value) return;
  const url = `${window.location.origin}/collection/${collection.value.id}`;
  try {
    await navigator.clipboard.writeText(url);
    isCopying.value = true;
    success('Collection share link copied!');
    setTimeout(() => {
      isCopying.value = false;
    }, 2000);
  } catch (err) {
    console.error(err);
  }
}

async function removeClip(clipId: string, e: Event) {
  e.stopPropagation();
  if (!collection.value) return;

  try {
    await api.removeClipFromCollection(collection.value.id, clipId);
    success('Clip removed from collection');
    await loadCollection();
    if (activeClipIndex.value >= clips.value.length) {
      activeClipIndex.value = Math.max(0, clips.value.length - 1);
    }
  } catch (err: any) {
    toastError(err.message || 'Failed to remove clip');
  }
}

async function moveClip(index: number, direction: 'up' | 'down', e: Event) {
  e.stopPropagation();
  if (!collection.value) return;

  const targetIndex = direction === 'up' ? index - 1 : index + 1;
  if (targetIndex < 0 || targetIndex >= clips.value.length) return;

  const newOrder = [...clips.value];
  const [moved] = newOrder.splice(index, 1);
  newOrder.splice(targetIndex, 0, moved);

  try {
    await api.reorderCollection(
      collection.value.id,
      newOrder.map((c) => c.id)
    );
    collection.value.clips = newOrder;
    activeClipIndex.value = targetIndex;
    success('Order updated');
  } catch (err: any) {
    toastError('Failed to reorder');
  }
}

async function deleteCollection() {
  if (!collection.value) return;
  if (!confirm(`Are you sure you want to delete collection "${collection.value.name}"?`)) return;

  try {
    await api.deleteCollection(collection.value.id);
    success('Collection deleted');
    router.push('/collections');
  } catch (err: any) {
    toastError(err.message || 'Failed to delete collection');
  }
}

function formatDuration(sec: number | null): string {
  if (!sec) return '0:00';
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

onMounted(() => {
  loadCollection();
});
</script>

<template>
  <div class="collection-detail-view">
    <div class="top-nav-bar">
      <router-link to="/collections" class="back-link">
        <ArrowLeft :size="16" />
        <span>All Collections</span>
      </router-link>
    </div>

    <div v-if="isLoading" class="loading-box">
      <div class="loader-spinner"></div>
      <p>Loading collection playlist...</p>
    </div>

    <div v-else-if="collection" class="collection-main">
      <!-- Collection Hero / Header -->
      <div class="collection-hero">
        <div class="hero-left">
          <div class="icon-bubble">
            <Folder :size="24" />
          </div>
          <div>
            <h1 class="col-title">{{ collection.name }}</h1>
            <p v-if="collection.description" class="col-description">{{ collection.description }}</p>
            <div class="col-meta">
              <span class="badge badge-violet">{{ clips.length }} clips</span>
              <span class="meta-dot">•</span>
              <span>Public Collection</span>
            </div>
          </div>
        </div>

        <div class="hero-actions">
          <button class="btn btn-primary" @click="copyShareLink">
            <Check v-if="isCopying" :size="15" />
            <Copy v-else :size="15" />
            <span>{{ isCopying ? 'Link Copied' : 'Share Collection' }}</span>
          </button>

          <template v-if="auth.isAuthenticated">
            <button class="btn btn-secondary btn-icon" title="Edit Collection" @click="isEditModalOpen = true">
              <Edit3 :size="16" />
            </button>
            <button class="btn btn-danger btn-icon" title="Delete Collection" @click="deleteCollection">
              <Trash2 :size="16" />
            </button>
          </template>
        </div>
      </div>

      <!-- Playlist Player Section -->
      <div v-if="activeClip" class="playlist-theater-layout">
        <div class="theater-player">
          <VideoPlayer
            :src="activeClip.videoUrl"
            :poster="activeClip.thumbnailUrl || undefined"
            :autoplay="true"
            @ended="onClipEnded"
          />

          <div class="playing-clip-details">
            <div class="playing-header">
              <div>
                <span class="now-playing-tag">
                  <Play :size="12" />
                  <span>Now Playing ({{ activeClipIndex + 1 }} of {{ clips.length }})</span>
                </span>
                <h2 class="playing-title">{{ activeClip.title }}</h2>
              </div>

              <router-link :to="`/clip/${activeClip.id}`" class="btn btn-secondary btn-sm">
                View Clip Page
              </router-link>
            </div>
            <p v-if="activeClip.description" class="playing-desc">{{ activeClip.description }}</p>
          </div>
        </div>

        <!-- Playlist sidebar -->
        <div class="playlist-sidebar">
          <div class="playlist-header">
            <h3>
              <Film :size="16" class="sidebar-icon" />
              <span>Collection Playlist</span>
            </h3>
            <span class="playlist-counter">{{ activeClipIndex + 1 }} / {{ clips.length }}</span>
          </div>

          <div class="playlist-scroll">
            <div
              v-for="(clip, idx) in clips"
              :key="clip.id"
              class="playlist-item"
              :class="{ active: idx === activeClipIndex }"
              @click="selectClip(idx)"
            >
              <span class="item-index">{{ idx + 1 }}</span>

              <div class="item-thumb-box">
                <img
                  :src="clip.thumbnailUrl || '/api/clips/' + clip.id + '/thumbnail'"
                  :alt="clip.title"
                  class="item-thumb"
                  loading="lazy"
                />
                <span v-if="clip.durationSeconds" class="item-dur">{{ formatDuration(clip.durationSeconds) }}</span>
              </div>

              <div class="item-text">
                <span class="item-title">{{ clip.title }}</span>
                <span class="item-meta">{{ clip.viewsCount }} views</span>
              </div>

              <!-- Reorder & delete for admin -->
              <div v-if="auth.isAuthenticated" class="item-admin-tools">
                <button
                  class="tool-btn"
                  :disabled="idx === 0"
                  title="Move up"
                  @click="moveClip(idx, 'up', $event)"
                >
                  <ChevronUp :size="13" />
                </button>
                <button
                  class="tool-btn"
                  :disabled="idx === clips.length - 1"
                  title="Move down"
                  @click="moveClip(idx, 'down', $event)"
                >
                  <ChevronDown :size="13" />
                </button>
                <button
                  class="tool-btn remove"
                  title="Remove from collection"
                  @click="removeClip(clip.id, $event)"
                >
                  &times;
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="empty-col-message">
        <Film :size="40" class="empty-icon" />
        <h3>This collection has no clips yet</h3>
        <p>Browse individual clips and click "Add to Collection" to populate this reel.</p>
        <router-link to="/" class="btn btn-secondary">Browse Clips</router-link>
      </div>
    </div>

    <!-- Edit modal -->
    <CollectionModal
      v-if="isEditModalOpen"
      :collection="collection"
      @close="isEditModalOpen = false"
      @saved="loadCollection"
    />
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.collection-detail-view {
  max-width: 1400px;
  margin: 0 auto;
  padding: 1.25rem 1.5rem 3rem;
  width: 100%;
}

.top-nav-bar {
  margin-bottom: 1rem;

  .back-link {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    color: $text-secondary;
    font-size: 0.85rem;
    text-decoration: none;

    &:hover {
      color: $accent-violet;
    }
  }
}

.collection-hero {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-xl;
  padding: 1.5rem 1.75rem;
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1.25rem;

  .hero-left {
    display: flex;
    align-items: center;
    gap: 1rem;

    .icon-bubble {
      width: 48px;
      height: 48px;
      border-radius: 50%;
      background: rgba(139, 92, 246, 0.15);
      color: #c4b5fd;
      border: 1px solid rgba(139, 92, 246, 0.3);
      display: flex;
      align-items: center;
      justify-content: center;
    }

    .col-title {
      font-size: 1.5rem;
      font-weight: 800;
    }

    .col-description {
      font-size: 0.88rem;
      color: $text-secondary;
      margin-top: 0.2rem;
    }

    .col-meta {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font-size: 0.8rem;
      color: $text-muted;
      margin-top: 0.35rem;

      .meta-dot {
        color: rgba(255, 255, 255, 0.15);
      }
    }
  }

  .hero-actions {
    display: flex;
    align-items: center;
    gap: 0.6rem;

    .btn-icon {
      padding: 0.55rem;
    }
  }
}

.playlist-theater-layout {
  display: grid;
  grid-template-columns: 1fr 380px;
  gap: 1.5rem;
  align-items: start;

  @media (max-width: 1024px) {
    grid-template-columns: 1fr;
  }
}

.theater-player {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

.playing-clip-details {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-lg;
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.65rem;

  .playing-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 1rem;
  }

  .now-playing-tag {
    display: inline-flex;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.75rem;
    font-weight: 700;
    color: $accent-cyan;
    text-transform: uppercase;
    letter-spacing: 0.05em;
    margin-bottom: 0.25rem;
  }

  .playing-title {
    font-size: 1.25rem;
    font-weight: 700;
  }

  .playing-desc {
    font-size: 0.9rem;
    color: $text-secondary;
    line-height: 1.5;
  }
}

.playlist-sidebar {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-lg;
  overflow: hidden;
  display: flex;
  flex-direction: column;

  .playlist-header {
    padding: 0.9rem 1.1rem;
    background: $bg-surface;
    border-bottom: 1px solid $border-subtle;
    display: flex;
    align-items: center;
    justify-content: space-between;

    h3 {
      font-size: 0.9rem;
      font-weight: 700;
      display: flex;
      align-items: center;
      gap: 0.45rem;

      .sidebar-icon {
        color: #c4b5fd;
      }
    }

    .playlist-counter {
      font-size: 0.78rem;
      color: $text-muted;
      font-weight: 600;
    }
  }

  .playlist-scroll {
    max-height: 600px;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
  }
}

.playlist-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.65rem 0.9rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.04);
  cursor: pointer;
  transition: all $transition-fast;

  &:hover {
    background: rgba(255, 255, 255, 0.04);
  }

  &.active {
    background: rgba(139, 92, 246, 0.12);
    border-left: 3px solid $accent-violet;

    .item-title {
      color: #c4b5fd;
      font-weight: 700;
    }
    .item-index {
      color: $accent-violet;
      font-weight: 700;
    }
  }

  .item-index {
    font-size: 0.78rem;
    color: $text-muted;
    width: 18px;
    text-align: center;
    flex-shrink: 0;
  }

  .item-thumb-box {
    position: relative;
    width: 72px;
    aspect-ratio: 16 / 9;
    border-radius: 4px;
    overflow: hidden;
    background: #000;
    flex-shrink: 0;

    .item-thumb {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .item-dur {
      position: absolute;
      bottom: 2px;
      right: 2px;
      font-size: 0.62rem;
      background: rgba(0, 0, 0, 0.8);
      padding: 0.1rem 0.25rem;
      border-radius: 2px;
      color: #fff;
    }
  }

  .item-text {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    gap: 0.2rem;

    .item-title {
      font-size: 0.83rem;
      font-weight: 500;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      color: $text-primary;
    }

    .item-meta {
      font-size: 0.72rem;
      color: $text-muted;
    }
  }

  .item-admin-tools {
    display: flex;
    align-items: center;
    gap: 0.2rem;
    flex-shrink: 0;

    .tool-btn {
      background: transparent;
      border: none;
      color: $text-muted;
      cursor: pointer;
      padding: 0.2rem;
      border-radius: 3px;

      &:hover:not(:disabled) {
        color: $text-primary;
        background: rgba(255, 255, 255, 0.1);
      }

      &.remove:hover {
        color: $accent-rose;
      }

      &:disabled {
        opacity: 0.25;
        cursor: not-allowed;
      }
    }
  }
}

.empty-col-message {
  text-align: center;
  padding: 4rem 1.5rem;
  background: $bg-card;
  border-radius: $radius-xl;
  border: 1px dashed $border-medium;
  max-width: 500px;
  margin: 2rem auto;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;

  .empty-icon {
    color: $text-muted;
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
    border: 3px solid rgba(139, 92, 246, 0.2);
    border-top-color: $accent-violet;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
  }
}
</style>
