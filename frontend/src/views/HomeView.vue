<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useRoute } from 'vue-router';
import { api, type Clip, type Collection } from '../api/client';
import ClipCard from '../components/ClipCard.vue';
import { useAuth } from '../stores/auth';
import { Film, Folder, Sparkles, RefreshCw, HardDrive, Globe, Link2, Lock } from '@lucide/vue';

const route = useRoute();
const { state: auth } = useAuth();

const clips = ref<Clip[]>([]);
const collections = ref<Collection[]>([]);
const totalCount = ref(0);
const currentPage = ref(1);
const pageSize = 24;
const isLoading = ref(true);
const selectedCollectionId = ref<string>('');
const selectedVisibility = ref<string>('');

const currentSearch = ref((route.query.q as string) || '');

async function loadClips() {
  isLoading.value = true;
  try {
    const res = await api.getClips({
      search: currentSearch.value || undefined,
      collectionId: selectedCollectionId.value || undefined,
      visibility: selectedVisibility.value || undefined,
      page: currentPage.value,
      pageSize,
    });
    clips.value = res.items;
    totalCount.value = res.totalCount;
  } catch (err) {
    console.error('Failed to load clips', err);
  } finally {
    isLoading.value = false;
  }
}

async function loadCollections() {
  try {
    collections.value = await api.getCollections();
  } catch (err) {
    console.error('Failed to load collections', err);
  }
}

function selectCollection(colId: string) {
  selectedCollectionId.value = colId;
  currentPage.value = 1;
  loadClips();
}

function selectVisibility(vis: string) {
  selectedVisibility.value = vis;
  currentPage.value = 1;
  loadClips();
}

onMounted(() => {
  loadClips();
  loadCollections();
});

watch(
  () => route.query.q,
  (newQ) => {
    currentSearch.value = (newQ as string) || '';
    currentPage.value = 1;
    loadClips();
  }
);
</script>

<template>
  <div class="home-view">
    <!-- Header Hero Banner -->
    <div class="hero-strip">
      <div class="hero-content">
        <div class="hero-heading">
          <h1>Game Clips Library</h1>
          <p>Instant, lightweight video sharing. Publicly viewable anywhere.</p>
        </div>

        <div class="quick-stats">
          <div class="stat-pill">
            <Film :size="15" class="stat-icon" />
            <span><strong>{{ totalCount }}</strong> clips</span>
          </div>
          <div class="stat-pill">
            <Folder :size="15" class="stat-icon" />
            <span><strong>{{ collections.length }}</strong> collections</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Filter Bar -->
    <div class="filter-bar">
      <div class="filter-chips">
        <button
          class="chip-btn"
          :class="{ active: selectedCollectionId === '' }"
          @click="selectCollection('')"
        >
          All Clips
        </button>

        <button
          v-for="col in collections"
          :key="col.id"
          class="chip-btn"
          :class="{ active: selectedCollectionId === col.id }"
          @click="selectCollection(col.id)"
        >
          <Folder :size="13" />
          <span>{{ col.name }}</span>
          <span class="chip-count">({{ col.clipCount }})</span>
        </button>
      </div>

      <button class="btn btn-ghost refresh-btn" title="Refresh clips list" @click="loadClips">
        <RefreshCw :size="15" :class="{ spinning: isLoading }" />
      </button>
    </div>

    <!-- Admin Visibility Filters -->
    <div v-if="auth.isAuthenticated" class="admin-vis-bar">
      <span class="admin-vis-label">Visibility:</span>
      <div class="admin-vis-chips">
        <button
          class="vis-chip"
          :class="{ active: selectedVisibility === '' }"
          @click="selectVisibility('')"
        >
          All
        </button>
        <button
          class="vis-chip"
          :class="{ active: selectedVisibility === 'public' }"
          @click="selectVisibility('public')"
        >
          <Globe :size="12" />
          <span>Public</span>
        </button>
        <button
          class="vis-chip"
          :class="{ active: selectedVisibility === 'unlisted' }"
          @click="selectVisibility('unlisted')"
        >
          <Link2 :size="12" />
          <span>Unlisted</span>
        </button>
        <button
          class="vis-chip"
          :class="{ active: selectedVisibility === 'private' }"
          @click="selectVisibility('private')"
        >
          <Lock :size="12" />
          <span>Private</span>
        </button>
      </div>
    </div>

    <!-- Search Active Notice -->
    <div v-if="currentSearch" class="search-notice">
      <span>Showing results for "<strong>{{ currentSearch }}</strong>"</span>
      <router-link to="/" class="clear-search-btn">Clear search</router-link>
    </div>

    <!-- Clips Grid -->
    <div v-if="isLoading && clips.length === 0" class="loading-state">
      <div class="loader-spinner"></div>
      <p>Loading gaming clips...</p>
    </div>

    <div v-else-if="clips.length > 0" class="clips-grid">
      <ClipCard v-for="clip in clips" :key="clip.id" :clip="clip" />
    </div>

    <!-- Empty State -->
    <div v-else class="empty-state">
      <div class="empty-icon-box">
        <Film :size="40" />
      </div>
      <h2>No clips found</h2>
      <p v-if="currentSearch || selectedCollectionId">
        No clips matched your filter. Try adjusting your query or selecting another collection.
      </p>
      <p v-else>
        Drop video files (.mp4, .webm, .mkv) into your configured <code>ingest</code> folder to automatically add them here, or log in to upload directly.
      </p>

      <div class="ingest-hint">
        <HardDrive :size="16" class="hint-icon" />
        <span><strong>Ingest Folder:</strong> Automatically monitors and imports new gameplay recordings.</span>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.home-view {
  max-width: 1400px;
  margin: 0 auto;
  padding: 1.5rem 1.5rem 3rem;
  width: 100%;
}

.hero-strip {
  background: linear-gradient(135deg, rgba(18, 23, 35, 0.8) 0%, rgba(13, 16, 24, 0.9) 100%);
  border: 1px solid $border-subtle;
  border-radius: $radius-xl;
  padding: 1.75rem 2rem;
  margin-bottom: 1.5rem;
  box-shadow: $shadow-card;
  position: relative;
  overflow: hidden;

  &::before {
    content: '';
    position: absolute;
    top: 0;
    right: 0;
    width: 300px;
    height: 100%;
    background: radial-gradient(circle at top right, rgba(0, 242, 254, 0.12), transparent 70%);
    pointer-events: none;
  }

  .hero-content {
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-wrap: wrap;
    gap: 1.25rem;
    position: relative;
    z-index: 1;

    .hero-heading {
      h1 {
        font-size: 1.75rem;
        font-weight: 800;
        letter-spacing: -0.03em;
        background: linear-gradient(135deg, #ffffff 40%, #cbd5e1 100%);
        -webkit-background-clip: text;
        -webkit-text-fill-color: transparent;
      }

      p {
        color: $text-secondary;
        font-size: 0.92rem;
        margin-top: 0.25rem;
      }
    }

    .quick-stats {
      display: flex;
      gap: 0.75rem;

      .stat-pill {
        display: flex;
        align-items: center;
        gap: 0.45rem;
        padding: 0.45rem 0.85rem;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid $border-subtle;
        border-radius: 20px;
        font-size: 0.85rem;
        color: $text-secondary;

        .stat-icon {
          color: $accent-cyan;
        }

        strong {
          color: $text-primary;
        }
      }
    }
  }
}

.filter-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.25rem;

  .filter-chips {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    overflow-x: auto;
    padding-bottom: 0.35rem;

    .chip-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      padding: 0.4rem 0.85rem;
      border-radius: 20px;
      font-size: 0.82rem;
      font-weight: 500;
      background: $bg-surface;
      border: 1px solid $border-subtle;
      color: $text-secondary;
      cursor: pointer;
      white-space: nowrap;
      transition: all $transition-fast;

      &:hover {
        background: $bg-surface-elevated;
        color: $text-primary;
      }

      &.active {
        background: rgba(0, 242, 254, 0.12);
        border-color: rgba(0, 242, 254, 0.4);
        color: $accent-cyan;
        font-weight: 600;
      }

      .chip-count {
        opacity: 0.6;
        font-size: 0.75rem;
      }
    }
  }

  .refresh-btn {
    padding: 0.45rem;
    border-radius: 50%;
  }
}

.admin-vis-bar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem 0.85rem;
  margin-bottom: 1.25rem;
  background: rgba(18, 23, 35, 0.6);
  border: 1px dashed rgba(255, 255, 255, 0.12);
  border-radius: $radius-md;

  .admin-vis-label {
    font-size: 0.78rem;
    font-weight: 700;
    color: $text-muted;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .admin-vis-chips {
    display: flex;
    align-items: center;
    gap: 0.4rem;

    .vis-chip {
      display: inline-flex;
      align-items: center;
      gap: 0.3rem;
      padding: 0.25rem 0.65rem;
      border-radius: $radius-sm;
      font-size: 0.75rem;
      font-weight: 600;
      background: $bg-input;
      border: 1px solid $border-subtle;
      color: $text-secondary;
      cursor: pointer;
      transition: all $transition-fast;

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
  }
}

.spinning {
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.search-notice {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1.25rem;
  font-size: 0.875rem;
  color: $text-secondary;

  .clear-search-btn {
    color: $accent-cyan;
    text-decoration: underline;
    cursor: pointer;
  }
}

.clips-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(290px, 1fr));
  gap: 1.5rem;
}

.loading-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 5rem 1rem;
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

.empty-state {
  text-align: center;
  padding: 4rem 1.5rem;
  background: $bg-card;
  border: 1px dashed $border-medium;
  border-radius: $radius-xl;
  max-width: 650px;
  margin: 2rem auto;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;

  .empty-icon-box {
    width: 72px;
    height: 72px;
    border-radius: 50%;
    background: rgba(255, 255, 255, 0.04);
    display: flex;
    align-items: center;
    justify-content: center;
    color: $text-muted;
    margin-bottom: 0.5rem;
  }

  h2 {
    font-size: 1.35rem;
    font-weight: 700;
  }

  p {
    font-size: 0.9rem;
    color: $text-secondary;
    line-height: 1.5;

    code {
      background: rgba(255, 255, 255, 0.1);
      padding: 0.15rem 0.4rem;
      border-radius: 4px;
      color: $accent-cyan;
    }
  }

  .ingest-hint {
    margin-top: 1rem;
    display: flex;
    align-items: center;
    gap: 0.5rem;
    font-size: 0.82rem;
    color: #a7f3d0;
    background: rgba(16, 185, 129, 0.08);
    border: 1px solid rgba(16, 185, 129, 0.2);
    padding: 0.6rem 1rem;
    border-radius: $radius-md;

    .hint-icon {
      color: $accent-emerald;
    }
  }
}
</style>
