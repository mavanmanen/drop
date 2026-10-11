<script setup lang="ts">
import { ref } from 'vue';
import type { Clip } from '../api/client';
import { useToast } from '../stores/toast';
import { Play, Copy, Check, Eye, Clock, Folder, Lock, Link2 } from '@lucide/vue';

const props = defineProps<{
  clip: Clip;
}>();

const { success } = useToast();
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
  border-radius: $radius-lg;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  transition: all $transition-smooth;

  &:hover {
    transform: translateY(-4px);
    border-color: $border-cyan;
    box-shadow: $shadow-card, 0 0 20px $accent-cyan-glow;

    .thumbnail-overlay {
      opacity: 1;
    }
    .thumbnail-img {
      transform: scale(1.04);
    }
    .copy-quick-btn {
      opacity: 1;
    }
  }
}

.thumbnail-wrapper {
  position: relative;
  aspect-ratio: 16 / 9;
  background: #0f121b;
  overflow: hidden;
  display: block;

  .thumbnail-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
    transition: transform 0.35s cubic-bezier(0.16, 1, 0.3, 1);
  }

  .thumbnail-overlay {
    position: absolute;
    inset: 0;
    background: rgba(11, 13, 19, 0.4);
    display: flex;
    align-items: center;
    justify-content: center;
    opacity: 0;
    transition: opacity $transition-fast;

    .play-bubble {
      width: 48px;
      height: 48px;
      border-radius: 50%;
      background: rgba(0, 242, 254, 0.9);
      color: #050811;
      display: flex;
      align-items: center;
      justify-content: center;
      box-shadow: 0 4px 16px rgba(0, 242, 254, 0.5);
      transform: scale(0.9);
      transition: transform $transition-fast;

      .play-icon {
        margin-left: 3px;
      }
    }
  }

  &:hover .play-bubble {
    transform: scale(1);
  }

  .time-badge {
    position: absolute;
    bottom: 0.6rem;
    right: 0.6rem;
    background: rgba(5, 7, 12, 0.85);
    backdrop-filter: blur(4px);
    padding: 0.2rem 0.45rem;
    border-radius: 4px;
    font-size: 0.72rem;
    font-weight: 600;
    color: #e2e8f0;
    display: flex;
    align-items: center;
    gap: 0.25rem;
  }

  .visibility-badge {
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

  .copy-quick-btn {
    position: absolute;
    top: 0.6rem;
    right: 0.6rem;
    background: rgba(15, 18, 27, 0.85);
    backdrop-filter: blur(4px);
    border: 1px solid $border-medium;
    color: #e2e8f0;
    padding: 0.35rem;
    border-radius: $radius-sm;
    cursor: pointer;
    opacity: 0.7;
    transition: all $transition-fast;
    display: flex;
    align-items: center;
    justify-content: center;

    &:hover {
      background: $accent-cyan;
      color: #050811;
      opacity: 1;
    }

    .check-icon {
      color: $accent-emerald;
    }
  }
}

.card-body {
  padding: 0.9rem 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  flex: 1;

  .clip-title-link {
    text-decoration: none;
    color: $text-primary;

    &:hover .clip-title {
      color: $accent-cyan;
    }
  }

  .clip-title {
    font-size: 0.95rem;
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
    gap: 0.4rem;
    font-size: 0.78rem;
    color: $text-muted;

    .meta-item {
      display: flex;
      align-items: center;
      gap: 0.25rem;
    }

    .meta-dot {
      color: rgba(255, 255, 255, 0.15);
    }
  }

  .collections-row {
    display: flex;
    flex-wrap: wrap;
    gap: 0.35rem;
    margin-top: 0.2rem;

    .col-tag {
      display: inline-flex;
      align-items: center;
      gap: 0.25rem;
      padding: 0.15rem 0.45rem;
      border-radius: 4px;
      font-size: 0.68rem;
      font-weight: 500;
      color: #cbd5e1;
      background: rgba(139, 92, 246, 0.12);
      border: 1px solid rgba(139, 92, 246, 0.25);
      text-decoration: none;
      transition: all $transition-fast;

      &:hover {
        background: rgba(139, 92, 246, 0.25);
        color: #fff;
      }
    }
  }

  .tags-badge-row {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 0.3rem;
    margin-top: 0.15rem;

    .card-tag-pill {
      font-size: 0.68rem;
      font-weight: 600;
      color: $accent-cyan;
      background: rgba(0, 242, 254, 0.08);
      border: 1px solid rgba(0, 242, 254, 0.2);
      padding: 0.12rem 0.4rem;
      border-radius: 4px;
      text-decoration: none;
      transition: all $transition-fast;

      &:hover {
        background: rgba(0, 242, 254, 0.18);
        border-color: $accent-cyan;
      }
    }

    .more-tags-indicator {
      font-size: 0.65rem;
      color: $text-muted;
      font-weight: 600;
    }
  }
}
</style>
