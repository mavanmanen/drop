import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useLikes } from '../useLikes';
import { api } from '../../api/client';

vi.mock('../../api/client', () => ({
  api: {
    likeClip: vi.fn(),
    unlikeClip: vi.fn(),
  },
}));

vi.mock('../../stores/toast', () => ({
  useToast: () => ({
    success: vi.fn(),
    error: vi.fn(),
  }),
}));

describe('useLikes composable', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('checks isLiked correctly and toggles like state', async () => {
    vi.mocked(api.likeClip).mockResolvedValue({ id: 'clip1', likesCount: 1, liked: true });

    const { isLiked, toggleLike } = useLikes();
    const mockClip = { id: 'clip1', likesCount: 0 };

    expect(isLiked('clip1')).toBe(false);

    // Like clip
    const liked = await toggleLike(mockClip);
    expect(liked).toBe(true);
    expect(mockClip.likesCount).toBe(1);
    expect(isLiked('clip1')).toBe(true);
    expect(api.likeClip).toHaveBeenCalledWith('clip1', true);

    // Unlike clip
    vi.mocked(api.likeClip).mockResolvedValue({ id: 'clip1', likesCount: 0, liked: false });
    const unliked = await toggleLike(mockClip);
    expect(unliked).toBe(false);
    expect(mockClip.likesCount).toBe(0);
    expect(isLiked('clip1')).toBe(false);
    expect(api.likeClip).toHaveBeenCalledWith('clip1', false);
  });

  it('reverts optimistic update when api call fails', async () => {
    vi.mocked(api.likeClip).mockRejectedValue(new Error('Network error'));

    const { isLiked, toggleLike } = useLikes();
    const mockClip = { id: 'clip_fail', likesCount: 5 };

    const result = await toggleLike(mockClip);

    expect(result).toBe(false);
    expect(mockClip.likesCount).toBe(5);
    expect(isLiked('clip_fail')).toBe(false);
  });
});
