<script setup lang="ts">
import { ref } from 'vue';
import type { Clip } from '../api/client';
import { useToast } from '../stores/toast';
import { Play, Copy, Check, Eye, Clock, Folder, Lock, Link2, Heart } from '@lucide/vue';
import { useLikes } from '../composables/useLikes';

const props = defineProps<{
  clip: Clip;
}>();

const { success } = useToast();
const { isLiked, toggleLike } = useLikes();
const copied = ref(false);

function formatDuration(sec: number | null): string {
  if (!sec) return '0:00';
  const m = Math.floor(sec / 60);
  const s = Math.floor(sec % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function formatDate(dateStr: string): string {
  try {
    const date = new Date(dateStr);
    const now = new Date();
    const diffSec = Math.floor((now.getTime() - date.getTime()) / 1000);

    if (diffSec < 60) return 'Just now';
    if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ago`;
    if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h ago`;
    if (diffSec < 604800) return `${Math.floor(diffSec / 86400)}d ago`;
    return date.toLocaleDateString();
  } catch {
    return dateStr;
  }
}

async function copyShareLink(e: Event) {
  e.preventDefault();
  e.stopPropagation();

  const shareUrl = `${window.location.origin}/clip/${props.clip.id}`;
  try {
    await navigator.clipboard.writeText(shareUrl);
    copied.value = true;
    success('Public link copied to clipboard!');
    setTimeout(() => {
      copied.value = false;
    }, 2000);
  } catch (err) {
    console.error('Failed to copy', err);
  }
}
</script>

<template>
  <div class="clip-card">
    <router-link :to="`/clip/${clip.id}`" class="thumbnail-wrapper">
      <img
        :src="clip.thumbnailUrl || '/api/clips/' + clip.id + '/thumbnail'"
        :alt="clip.title"
        class="thumbnail-img"
        loading="lazy"
      />

      <div class="thumbnail-overlay">
        <div class="play-bubble">
          <Play :size="24" class="play-icon" />
        </div>
      </div>

      <!-- Visibility badge (unlisted or private) -->
      <div v-if="clip.visibility !== 'public'" class="visibility-badge" :class="`vis-${clip.visibility}`">
        <Lock v-if="clip.visibility === 'private'" :size="10" />
        <Link2 v-else :size="10" />
        <span>{{ clip.visibility }}</span>
      </div>

      <!-- Duration badge -->
      <div v-if="clip.durationSeconds" class="time-badge">
        <Clock :size="11" />
        <span>{{ formatDuration(clip.durationSeconds) }}</span>
      </div>

      <!-- Quick copy button -->
      <button
        class="copy-quick-btn"
        :title="copied ? 'Copied!' : 'Copy public link'"
        @click="copyShareLink"
      >
        <Check v-if="copied" :size="14" class="check-icon" />
        <Copy v-else :size="14" />
      </button>
    </router-link>

    <div class="card-body">
      <router-link :to="`/clip/${clip.id}`" class="clip-title-link">
        <h3 class="clip-title">{{ clip.title }}</h3>
      </router-link>

      <div class="meta-row">
        <span class="meta-item">
          <Eye :size="13" />
          <span>{{ clip.viewsCount }} views</span>
        </span>
        <span class="meta-dot">•</span>
        <button
          class="card-like-btn"
          :class="{ 'is-liked': isLiked(clip.id) }"
          :title="isLiked(clip.id) ? 'Unlike this clip' : 'Like this clip'"
          @click.prevent.stop="toggleLike(clip)"
        >
          <Heart :size="12" class="card-like-icon" :class="{ 'fill-current': isLiked(clip.id) }" />
          <span>{{ clip.likesCount }}</span>
        </button>
        <span class="meta-dot">•</span>
        <span class="meta-item">{{ formatDate(clip.createdAtUtc) }}</span>
      </div>

      <!-- Collections tags -->
      <div v-if="clip.collections && clip.collections.length > 0" class="collections-row">
        <router-link
          v-for="col in clip.collections"
          :key="col.id"
          :to="`/collection/${col.id}`"
          class="col-tag"
        >
          <Folder :size="10" />
          <span>{{ col.name }}</span>
        </router-link>
      </div>

      <!-- Tags row -->
      <div v-if="clip.tags && clip.tags.length > 0" class="tags-badge-row">
        <router-link
          v-for="t in clip.tags.slice(0, 3)"
          :key="t"
          :to="`/?tag=${encodeURIComponent(t)}`"
          class="card-tag-pill"
        >
          <span>#{{ t }}</span>
        </router-link>
        <span v-if="clip.tags.length > 3" class="more-tags-indicator">
          +{{ clip.tags.length - 3 }}
        </span>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.clip-card {
  background: $bg-card;
  border: 1px solid $border-subtle;
  border-radius: $radius-md;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  transition: border-color $transition-fast, background-color $transition-fast;

  &:hover {
    border-color: rgba(255, 255, 255, 0.2);
    background: $bg-card-hover;

    .thumbnail-overlay {
      opacity: 1;
    }
    .copy-quick-btn {
      opacity: 1;
    }
  }
}

.thumbnail-wrapper {
  position: relative;
  aspect-ratio: 16 / 9;
  background: #0b0d13;
  overflow: hidden;
  display: block;

  .thumbnail-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .thumbnail-overlay {
    position: absolute;
    inset: 0;
    background: rgba(0, 0, 0, 0.35);
    display: flex;
    align-items: center;
    justify-content: center;
    opacity: 0;
    transition: opacity $transition-fast;

    .play-bubble {
      width: 40px;
      height: 40px;
      border-radius: 50%;
      background: #ffffff;
      color: #000000;
      display: flex;
      align-items: center;
      justify-content: center;

      .play-icon {
        margin-left: 2px;
      }
    }
  }

  .time-badge {
    position: absolute;
    bottom: 0.5rem;
    right: 0.5rem;
    background: rgba(0, 0, 0, 0.85);
    padding: 0.15rem 0.4rem;
    border-radius: $radius-xs;
    font-size: 0.7rem;
    font-weight: 500;
    color: #f1f5f9;
    display: flex;
    align-items: center;
    gap: 0.25rem;
  }

  .visibility-badge {
    position: absolute;
    top: 0.5rem;
    left: 0.5rem;
    padding: 0.15rem 0.4rem;
    border-radius: $radius-xs;
    font-size: 0.65rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    display: flex;
    align-items: center;
    gap: 0.25rem;
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

  .copy-quick-btn {
    position: absolute;
    top: 0.5rem;
    right: 0.5rem;
    background: rgba(0, 0, 0, 0.75);
    border: 1px solid rgba(255, 255, 255, 0.15);
    color: #f1f5f9;
    padding: 0.3rem;
    border-radius: $radius-xs;
    cursor: pointer;
    opacity: 0;
    transition: background-color $transition-fast, color $transition-fast, opacity $transition-fast;
    display: flex;
    align-items: center;
    justify-content: center;

    &:hover {
      background: #ffffff;
      color: #000000;
      opacity: 1;
    }

    .check-icon {
      color: $accent-emerald;
    }
  }
}

.card-body {
  padding: 0.75rem 0.85rem;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  flex: 1;

  .clip-title-link {
    text-decoration: none;
    color: $text-primary;

    &:hover .clip-title {
      color: #38bdf8;
    }
  }

  .clip-title {
    font-size: 0.875rem;
    font-weight: 600;
    line-height: 1.35;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
    transition: color $transition-fast;
  }

  .meta-row {
    display: flex;
    align-items: center;
    gap: 0.35rem;
    font-size: 0.75rem;
    color: $text-muted;

    .meta-item {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .card-like-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      background: none;
      border: none;
      padding: 0.1rem 0.25rem;
      border-radius: $radius-xs;
      font-size: 0.75rem;
      color: $text-muted;
      cursor: pointer;
      transition: color $transition-fast, background-color $transition-fast;

      &:hover {
        color: #f43f5e;
        background: rgba(244, 63, 94, 0.1);
      }

      &.is-liked {
        color: #f43f5e;

        .card-like-icon {
          fill: #f43f5e;
        }
      }
    }

    .meta-dot {
      color: rgba(255, 255, 255, 0.15);
    }
  }

  .collections-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.3rem;
    margin-top: 0.15rem;

    .col-tag {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.1rem 0.35rem;
      border-radius: $radius-xs;
      font-size: 0.68rem;
      font-weight: 500;
      color: #cbd5e1;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid $border-subtle;
      text-decoration: none;
      transition: background-color $transition-fast, color $transition-fast;

      &:hover {
        background: rgba(255, 255, 255, 0.12);
        color: #fff;
      }
    }
  }

  .tags-badge-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.25rem;
    margin-top: 0.1rem;

    .card-tag-pill {
      font-size: 0.68rem;
      font-weight: 500;
      color: $text-secondary;
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid $border-subtle;
      padding: 0.08rem 0.35rem;
      border-radius: $radius-xs;
      text-decoration: none;
      transition: color $transition-fast, border-color $transition-fast;

      &:hover {
        color: #38bdf8;
        border-color: rgba(56, 189, 248, 0.4);
      }
    }

    .more-tags-indicator {
      font-size: 0.65rem;
      color: $text-muted;
      font-weight: 500;
    }
  }
}
</style>
