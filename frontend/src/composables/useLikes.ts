import { ref } from 'vue';
import { api } from '../api/client';
import { useToast } from '../stores/toast';

const STORAGE_KEY = 'drop_liked_clips';

function loadLikedClips(): Set<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const arr = JSON.parse(raw);
      if (Array.isArray(arr)) {
        return new Set(arr);
      }
    }
  } catch (err) {
    console.error('Failed to load liked clips from storage', err);
  }
  return new Set();
}

function persistLikedClips(set: Set<string>) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(Array.from(set)));
  } catch (err) {
    console.error('Failed to save liked clips to storage', err);
  }
}

// Global reactive state shared across components
const likedSet = ref<Set<string>>(loadLikedClips());

export function useLikes() {
  const { success, error: toastError } = useToast();

  function isLiked(clipId: string): boolean {
    return likedSet.value.has(clipId);
  }

  async function toggleLike(clip: { id: string; likesCount: number }): Promise<boolean> {
    const currentlyLiked = isLiked(clip.id);
    const targetLiked = !currentlyLiked;

    // Optimistic update
    const previousCount = clip.likesCount;
    if (targetLiked) {
      clip.likesCount = previousCount + 1;
      likedSet.value.add(clip.id);
    } else {
      clip.likesCount = Math.max(0, previousCount - 1);
      likedSet.value.delete(clip.id);
    }
    // Trigger reactivity for set
    likedSet.value = new Set(likedSet.value);
    persistLikedClips(likedSet.value);

    try {
      const res = await api.likeClip(clip.id, targetLiked);
      clip.likesCount = res.likesCount;
      if (targetLiked) {
        success('Liked clip!');
      }
      return targetLiked;
    } catch (err: any) {
      console.error('Failed to toggle like', err);
      // Revert optimistic update
      clip.likesCount = previousCount;
      if (currentlyLiked) {
        likedSet.value.add(clip.id);
      } else {
        likedSet.value.delete(clip.id);
      }
      likedSet.value = new Set(likedSet.value);
      persistLikedClips(likedSet.value);

      toastError(err.message || 'Failed to update like');
      return currentlyLiked;
    }
  }

  return {
    isLiked,
    toggleLike,
  };
}
