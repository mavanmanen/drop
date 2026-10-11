<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { api, type Collection } from '../api/client';
import { useAuth } from '../stores/auth';
import { useToast } from '../stores/toast';
import CollectionModal from '../components/CollectionModal.vue';
import { Folder, FolderPlus, Film, Edit3, Trash2, ArrowRight, Lock, Link2 } from '@lucide/vue';

const { state: auth } = useAuth();
const { success, error: toastError } = useToast();

const collections = ref<Collection[]>([]);
const isLoading = ref(true);

const isModalOpen = ref(false);
const editingCollection = ref<Collection | null>(null);

async function loadCollections() {
  isLoading.value = true;
  try {
    collections.value = await api.getCollections();
  } catch (err) {
    console.error(err);
  } finally {
    isLoading.value = false;
  }
}

function openCreate() {
  editingCollection.value = null;
  isModalOpen.value = true;
}

function openEdit(col: Collection, e: Event) {
  e.preventDefault();
  e.stopPropagation();
  editingCollection.value = col;
  isModalOpen.value = true;
}

async function deleteCollection(col: Collection, e: Event) {
  e.preventDefault();
  e.stopPropagation();

  if (!confirm(`Are you sure you want to delete the collection "${col.name}"? The clips will remain in the library.`)) {
    return;
  }

  try {
    await api.deleteCollection(col.id);
    success(`Collection "${col.name}" deleted`);
    collections.value = collections.value.filter((c) => c.id !== col.id);
  } catch (err: any) {
    toastError(err.message || 'Failed to delete collection');
  }
}

function onSaved() {
  loadCollections();
}

onMounted(() => {
  loadCollections();
});
</script>

<template>
  <div class="collections-view">
    <div class="collections-header">
      <div>
        <h1>Clip Collections</h1>
        <p>Curated compilations and highlights grouped by game, event, or theme.</p>
      </div>

      <button v-if="auth.isAuthenticated" class="btn btn-primary" @click="openCreate">
        <FolderPlus :size="16" />
        <span>New Collection</span>
      </button>
    </div>

    <!-- Collections Grid -->
    <div v-if="isLoading" class="loading-state">
      <div class="loader-spinner"></div>
      <p>Loading collections...</p>
    </div>

    <div v-else-if="collections.length > 0" class="collections-grid">
      <router-link
        v-for="col in collections"
        :key="col.id"
        :to="`/collection/${col.id}`"
        class="collection-card"
      >
        <div class="card-cover">
          <img
            v-if="col.coverThumbnailUrl"
            :src="col.coverThumbnailUrl"
            :alt="col.name"
            class="cover-img"
            loading="lazy"
          />
          <div v-else class="cover-placeholder">
            <Folder :size="40" class="folder-icon" />
          </div>

          <div class="cover-overlay"></div>

          <div class="clip-count-pill">
            <Film :size="12" />
            <span>{{ col.clipCount }} {{ col.clipCount === 1 ? 'clip' : 'clips' }}</span>
          </div>

          <!-- Visibility badge -->
          <div v-if="col.visibility !== 'public'" class="visibility-pill" :class="`vis-${col.visibility}`">
            <Lock v-if="col.visibility === 'private'" :size="10" />
            <Link2 v-else :size="10" />
            <span>{{ col.visibility }}</span>
          </div>

          <!-- Admin actions -->
          <div v-if="auth.isAuthenticated" class="card-admin-actions">
            <button
              class="admin-act-btn"
              title="Edit collection"
              @click="openEdit(col, $event)"
            >
              <Edit3 :size="13" />
            </button>
            <button
              class="admin-act-btn delete"
              title="Delete collection"
              @click="deleteCollection(col, $event)"
            >
              <Trash2 :size="13" />
            </button>
          </div>
        </div>

        <div class="card-content">
          <h2 class="col-name">{{ col.name }}</h2>
          <p v-if="col.description" class="col-desc">{{ col.description }}</p>
          <div class="view-more">
            <span>View collection</span>
            <ArrowRight :size="14" />
          </div>
        </div>
      </router-link>
    </div>

    <div v-else class="empty-state">
      <Folder :size="44" class="empty-icon" />
      <h2>No collections yet</h2>
      <p>Create a collection to organize your gaming highlights into playlists.</p>
      <button v-if="auth.isAuthenticated" class="btn btn-primary" @click="openCreate">
        <FolderPlus :size="16" />
        <span>Create First Collection</span>
      </button>
    </div>

    <!-- Collection Modal -->
    <CollectionModal
      v-if="isModalOpen"
      :collection="editingCollection"
      @close="isModalOpen = false"
      @saved="onSaved"
    />
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.collections-view {
  max-width: 1400px;
  margin: 0 auto;
  padding: 1.5rem 1.5rem 3rem;
  width: 100%;
}

.collections-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 2rem;

  h1 {
    font-size: 1.75rem;
    font-weight: 800;
    letter-spacing: -0.03em;
  }

  p {
    font-size: 0.92rem;
    color: $text-secondary;
    margin-top: 0.25rem;
  }
}

.collections-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.5rem;
}

.collection-card {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-lg;
  overflow: hidden;
  text-decoration: none;
  color: $text-primary;
  display: flex;
  flex-direction: column;
  transition: all $transition-smooth;

  &:hover {
    transform: translateY(-4px);
    border-color: rgba(139, 92, 246, 0.5);
    box-shadow: $shadow-card, 0 0 20px $accent-violet-glow;

    .cover-img {
      transform: scale(1.05);
    }
    .view-more {
      color: #c4b5fd;
      transform: translateX(3px);
    }
  }
}

.card-cover {
  position: relative;
  aspect-ratio: 16 / 9;
  background: #0f121b;
  overflow: hidden;

  .cover-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    transition: transform 0.35s ease;
  }

  .cover-placeholder {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    color: $text-muted;
    background: radial-gradient(circle at center, rgba(139, 92, 246, 0.15), transparent 70%);
  }

  .cover-overlay {
    position: absolute;
    inset: 0;
    background: linear-gradient(to top, rgba(11, 13, 19, 0.7) 0%, transparent 60%);
  }

  .clip-count-pill {
    position: absolute;
    bottom: 0.75rem;
    left: 0.75rem;
    background: rgba(11, 13, 19, 0.85);
    backdrop-filter: blur(4px);
    border: 1px solid $border-subtle;
    padding: 0.2rem 0.55rem;
    border-radius: 20px;
    font-size: 0.75rem;
    font-weight: 600;
    color: #e2e8f0;
    display: flex;
    align-items: center;
    gap: 0.35rem;
  }

  .visibility-pill {
    position: absolute;
    top: 0.6rem;
    left: 0.6rem;
    padding: 0.2rem 0.5rem;
    border-radius: 4px;
    font-size: 0.68rem;
    font-weight: 700;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    display: flex;
    align-items: center;
    gap: 0.3rem;
    backdrop-filter: blur(4px);
    z-index: 5;

    &.vis-unlisted {
      background: rgba(245, 158, 11, 0.9);
      color: #0f172a;
    }

    &.vis-private {
      background: rgba(244, 63, 94, 0.9);
      color: #ffffff;
    }
  }

  .card-admin-actions {
    position: absolute;
    top: 0.6rem;
    right: 0.6rem;
    display: flex;
    gap: 0.35rem;

    .admin-act-btn {
      background: rgba(15, 18, 27, 0.85);
      backdrop-filter: blur(4px);
      border: 1px solid $border-medium;
      color: #e2e8f0;
      padding: 0.35rem;
      border-radius: $radius-sm;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;

      &:hover {
        background: #252d40;
        color: #fff;
      }

      &.delete:hover {
        background: $accent-rose;
      }
    }
  }
}

.card-content {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  flex: 1;

  .col-name {
    font-size: 1.1rem;
    font-weight: 700;
    line-height: 1.3;
  }

  .col-desc {
    font-size: 0.85rem;
    color: $text-secondary;
    line-height: 1.5;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  .view-more {
    margin-top: auto;
    padding-top: 0.75rem;
    display: flex;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.82rem;
    font-weight: 600;
    color: $text-muted;
    transition: all $transition-fast;
  }
}

.empty-state {
  text-align: center;
  padding: 5rem 1.5rem;
  background: $bg-card;
  border: 1px dashed $border-medium;
  border-radius: $radius-xl;
  max-width: 550px;
  margin: 2rem auto;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.75rem;

  .empty-icon {
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
    margin-bottom: 0.5rem;
  }
}

.loading-state {
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
