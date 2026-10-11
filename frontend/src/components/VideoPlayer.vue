<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, watch } from 'vue';
import {
  Play,
  Pause,
  Volume2,
  VolumeX,
  Maximize,
  Minimize,
  Repeat,
  RotateCcw,
  Settings
} from '@lucide/vue';

const props = withDefaults(
  defineProps<{
    src: string;
    poster?: string;
    autoplay?: boolean;
    loopDefault?: boolean;
  }>(),
  {
    autoplay: true,
    loopDefault: true,
  }
);

const emit = defineEmits<{
  (e: 'ended'): void;
}>();

const videoRef = ref<HTMLVideoElement | null>(null);
const containerRef = ref<HTMLDivElement | null>(null);

const isPlaying = ref(false);
const currentTime = ref(0);
const duration = ref(0);
const volume = ref(1);
const isMuted = ref(false);
const isLooping = ref(props.loopDefault);
const isFullscreen = ref(false);
const playbackRate = ref(1);
const showSpeedMenu = ref(false);
const showControls = ref(true);
const isBuffering = ref(false);

let hideControlsTimeout: number | null = null;

const speeds = [0.5, 0.75, 1, 1.25, 1.5, 2];

function formatTime(seconds: number): string {
  if (isNaN(seconds)) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

function togglePlay() {
  if (!videoRef.value) return;
  if (videoRef.value.paused) {
    videoRef.value.play();
  } else {
    videoRef.value.pause();
  }
}

function toggleMute() {
  if (!videoRef.value) return;
  videoRef.value.muted = !videoRef.value.muted;
  isMuted.value = videoRef.value.muted;
}

function onVolumeChange(e: Event) {
  const val = parseFloat((e.target as HTMLInputElement).value);
  volume.value = val;
  if (videoRef.value) {
    videoRef.value.volume = val;
    videoRef.value.muted = val === 0;
    isMuted.value = val === 0;
  }
}

function toggleLoop() {
  isLooping.value = !isLooping.value;
  if (videoRef.value) {
    videoRef.value.loop = isLooping.value;
  }
}

function setSpeed(spd: number) {
  playbackRate.value = spd;
  if (videoRef.value) {
    videoRef.value.playbackRate = spd;
  }
  showSpeedMenu.value = false;
}

function toggleFullscreen() {
  if (!containerRef.value) return;
  if (!document.fullscreenElement) {
    containerRef.value.requestFullscreen().catch(() => {});
  } else {
    document.exitFullscreen().catch(() => {});
  }
}

function onSeek(e: Event) {
  const val = parseFloat((e.target as HTMLInputElement).value);
  if (videoRef.value) {
    videoRef.value.currentTime = val;
  }
}

function onMouseMove() {
  showControls.value = true;
  if (hideControlsTimeout) clearTimeout(hideControlsTimeout);
  if (isPlaying.value) {
    hideControlsTimeout = window.setTimeout(() => {
      showControls.value = false;
      showSpeedMenu.value = false;
    }, 2500);
  }
}

function handleKeyDown(e: KeyboardEvent) {
  // Ignore if user is typing in an input
  const tag = (e.target as HTMLElement)?.tagName?.toLowerCase();
  if (tag === 'input' || tag === 'textarea') return;

  if (e.code === 'Space' || e.key === 'k') {
    e.preventDefault();
    togglePlay();
  } else if (e.key === 'f') {
    e.preventDefault();
    toggleFullscreen();
  } else if (e.key === 'm') {
    e.preventDefault();
    toggleMute();
  } else if (e.key === 'l') {
    e.preventDefault();
    toggleLoop();
  } else if (e.key === 'ArrowRight') {
    e.preventDefault();
    if (videoRef.value) videoRef.value.currentTime = Math.min(duration.value, currentTime.value + 5);
  } else if (e.key === 'ArrowLeft') {
    e.preventDefault();
    if (videoRef.value) videoRef.value.currentTime = Math.max(0, currentTime.value - 5);
  } else if (e.key === 'ArrowUp') {
    e.preventDefault();
    const newVol = Math.min(1, volume.value + 0.1);
    volume.value = newVol;
    if (videoRef.value) videoRef.value.volume = newVol;
  } else if (e.key === 'ArrowDown') {
    e.preventDefault();
    const newVol = Math.max(0, volume.value - 0.1);
    volume.value = newVol;
    if (videoRef.value) videoRef.value.volume = newVol;
  }
}

function onFullscreenChange() {
  isFullscreen.value = !!document.fullscreenElement;
}

onMounted(() => {
  window.addEventListener('keydown', handleKeyDown);
  document.addEventListener('fullscreenchange', onFullscreenChange);

  if (videoRef.value) {
    videoRef.value.loop = isLooping.value;
  }
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleKeyDown);
  document.removeEventListener('fullscreenchange', onFullscreenChange);
  if (hideControlsTimeout) clearTimeout(hideControlsTimeout);
});

watch(
  () => props.src,
  () => {
    currentTime.value = 0;
    if (videoRef.value) {
      videoRef.value.load();
    }
  }
);
</script>

<template>
  <div
    ref="containerRef"
    class="video-container"
    :class="{ 'hide-cursor': !showControls && isPlaying, 'is-fullscreen': isFullscreen }"
    @mousemove="onMouseMove"
    @mouseleave="isPlaying && (showControls = false)"
  >
    <video
      ref="videoRef"
      class="video-element"
      :src="src"
      :poster="poster"
      :autoplay="autoplay"
      playsinline
      @click="togglePlay"
      @play="isPlaying = true"
      @pause="isPlaying = false"
      @timeupdate="currentTime = videoRef?.currentTime || 0"
      @loadedmetadata="duration = videoRef?.duration || 0"
      @waiting="isBuffering = true"
      @playing="isBuffering = false"
      @ended="emit('ended')"
    ></video>

    <!-- Buffering Spinner -->
    <div v-if="isBuffering" class="buffering-spinner">
      <div class="spinner-ring"></div>
    </div>

    <!-- Center Play/Pause indicator -->
    <div
      class="center-indicator"
      :class="{ visible: !isPlaying && !isBuffering }"
      @click="togglePlay"
    >
      <div class="indicator-icon-box">
        <Play :size="36" class="play-offset" />
      </div>
    </div>

    <!-- Controls Overlay -->
    <div class="controls-overlay" :class="{ visible: showControls || !isPlaying }">
      <!-- Scrub Bar -->
      <div class="scrub-container">
        <input
          type="range"
          min="0"
          :max="duration || 100"
          step="0.01"
          :value="currentTime"
          class="scrub-range"
          @input="onSeek"
        />
        <div
          class="scrub-progress"
          :style="{ width: duration ? `${(currentTime / duration) * 100}%` : '0%' }"
        ></div>
      </div>

      <!-- Controls Bottom Bar -->
      <div class="controls-bar">
        <div class="left-controls">
          <!-- Play / Pause -->
          <button class="ctrl-btn" :title="isPlaying ? 'Pause (Space)' : 'Play (Space)'" @click="togglePlay">
            <Pause v-if="isPlaying" :size="20" />
            <Play v-else :size="20" />
          </button>

          <!-- Volume & Mute -->
          <div class="volume-group">
            <button class="ctrl-btn" :title="isMuted ? 'Unmute (M)' : 'Mute (M)'" @click="toggleMute">
              <VolumeX v-if="isMuted || volume === 0" :size="20" />
              <Volume2 v-else :size="20" />
            </button>
            <input
              type="range"
              min="0"
              max="1"
              step="0.05"
              :value="isMuted ? 0 : volume"
              class="volume-slider"
              @input="onVolumeChange"
            />
          </div>

          <!-- Time Display -->
          <div class="time-display">
            <span>{{ formatTime(currentTime) }}</span>
            <span class="divider">/</span>
            <span>{{ formatTime(duration) }}</span>
          </div>
        </div>

        <div class="right-controls">
          <!-- Loop Toggle -->
          <button
            class="ctrl-btn"
            :class="{ active: isLooping }"
            :title="isLooping ? 'Loop: On (L)' : 'Loop: Off (L)'"
            @click="toggleLoop"
          >
            <Repeat :size="18" />
          </button>

          <!-- Speed Selector -->
          <div class="speed-menu-wrapper">
            <button
              class="ctrl-btn speed-btn"
              title="Playback Speed"
              @click="showSpeedMenu = !showSpeedMenu"
            >
              <span>{{ playbackRate }}x</span>
            </button>
            <div v-if="showSpeedMenu" class="speed-popup">
              <button
                v-for="s in speeds"
                :key="s"
                class="speed-option"
                :class="{ active: playbackRate === s }"
                @click="setSpeed(s)"
              >
                {{ s }}x
              </button>
            </div>
          </div>

          <!-- Fullscreen -->
          <button
            class="ctrl-btn"
            :title="isFullscreen ? 'Exit Fullscreen (F)' : 'Fullscreen (F)'"
            @click="toggleFullscreen"
          >
            <Minimize v-if="isFullscreen" :size="19" />
            <Maximize v-else :size="19" />
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="scss">
@use '../assets/styles/variables' as *;

.video-container {
  position: relative;
  width: 100%;
  aspect-ratio: 16 / 9;
  background: #000;
  border-radius: $radius-lg;
  overflow: hidden;
  box-shadow: 0 12px 36px rgba(0, 0, 0, 0.7);
  user-select: none;

  &.is-fullscreen {
    border-radius: 0;
    width: 100vw;
    height: 100vh;
  }

  &.hide-cursor {
    cursor: none;
  }
}

.video-element {
  width: 100%;
  height: 100%;
  object-fit: contain;
  display: block;
}

.buffering-spinner {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  pointer-events: none;

  .spinner-ring {
    width: 50px;
    height: 50px;
    border: 3px solid rgba(0, 242, 254, 0.2);
    border-top-color: $accent-cyan;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
  }
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.center-indicator {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.2s ease;

  &.visible {
    opacity: 1;
    pointer-events: auto;
  }

  .indicator-icon-box {
    width: 72px;
    height: 72px;
    border-radius: 50%;
    background: rgba(0, 242, 254, 0.85);
    color: #050811;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    box-shadow: 0 0 30px rgba(0, 242, 254, 0.6);
    transition: transform $transition-fast;

    &:hover {
      transform: scale(1.08);
    }

    .play-offset {
      margin-left: 4px;
    }
  }
}

.controls-overlay {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  background: linear-gradient(to top, rgba(0, 0, 0, 0.85) 0%, rgba(0, 0, 0, 0.2) 25%, transparent 60%);
  opacity: 0;
  transition: opacity 0.25s ease;
  pointer-events: none;

  &.visible {
    opacity: 1;
    pointer-events: auto;
  }
}

.scrub-container {
  position: relative;
  width: calc(100% - 1.5rem);
  margin: 0 auto;
  height: 6px;
  cursor: pointer;

  &:hover {
    height: 10px;

    .scrub-range::-webkit-slider-thumb {
      transform: scale(1.2);
      opacity: 1;
    }
  }

  .scrub-range {
    position: absolute;
    inset: 0;
    width: 100%;
    height: 100%;
    opacity: 0;
    z-index: 10;
    cursor: pointer;
    margin: 0;
  }

  .scrub-progress {
    position: absolute;
    top: 0;
    left: 0;
    height: 100%;
    background: linear-gradient(90deg, $accent-cyan 0%, #38bdf8 100%);
    border-radius: 3px;
    box-shadow: 0 0 10px rgba(0, 242, 254, 0.5);
    pointer-events: none;
  }

  &::before {
    content: '';
    position: absolute;
    inset: 0;
    background: rgba(255, 255, 255, 0.25);
    border-radius: 3px;
  }
}

.controls-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.5rem 1rem 0.75rem;

  .left-controls, .right-controls {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }
}

.ctrl-btn {
  background: transparent;
  border: none;
  color: #f1f5f9;
  cursor: pointer;
  padding: 0.35rem;
  border-radius: $radius-sm;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all $transition-fast;

  &:hover {
    color: $accent-cyan;
    transform: scale(1.08);
  }

  &.active {
    color: $accent-cyan;
    background: rgba(0, 242, 254, 0.15);
  }
}

.volume-group {
  display: flex;
  align-items: center;
  gap: 0.4rem;

  .volume-slider {
    width: 65px;
    height: 4px;
    accent-color: $accent-cyan;
    cursor: pointer;
  }
}

.time-display {
  font-size: 0.8rem;
  font-weight: 500;
  color: #cbd5e1;
  display: flex;
  align-items: center;
  gap: 0.3rem;

  .divider {
    color: #64748b;
  }
}

.speed-menu-wrapper {
  position: relative;

  .speed-btn {
    font-size: 0.8rem;
    font-weight: 700;
    padding: 0.2rem 0.45rem;
  }

  .speed-popup {
    position: absolute;
    bottom: 120%;
    right: 0;
    background: #141824;
    border: 1px solid $border-medium;
    border-radius: $radius-md;
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.5);
    padding: 0.3rem;
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
    z-index: 50;

    .speed-option {
      background: transparent;
      border: none;
      color: $text-secondary;
      padding: 0.3rem 0.8rem;
      border-radius: $radius-sm;
      font-size: 0.78rem;
      cursor: pointer;
      text-align: left;

      &:hover {
        background: rgba(255, 255, 255, 0.08);
        color: #fff;
      }

      &.active {
        color: $accent-cyan;
        font-weight: 700;
      }
    }
  }
}
</style>
