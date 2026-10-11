import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';
import HomeView from '../HomeView.vue';
import { api } from '../../api/client';
import { useAuth } from '../../stores/auth';

vi.mock('vue-router', () => ({
  useRoute: () => ({
    query: {},
  }),
}));

const stubs = {
  'router-link': {
    template: '<a><slot /></a>',
  },
  ClipCard: {
    template: '<div class="clip-card-stub">{{ clip.title }}</div>',
    props: ['clip'],
  },
};

describe('HomeView.vue', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(api, 'getClips').mockResolvedValue({
      items: [
        {
          id: 'c1',
          title: 'First Clip',
          description: null,
          originalFileName: 'c1.mp4',
          fileSizeBytes: 100,
          contentType: 'video/mp4',
          durationSeconds: 15,
          hasThumbnail: false,
          videoUrl: '/api/clips/c1/stream',
          thumbnailUrl: '/api/clips/c1/thumbnail',
          createdAtUtc: new Date().toISOString(),
          viewsCount: 1,
          visibility: 'public',
          collections: [],
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 24,
    });
    vi.spyOn(api, 'getCollections').mockResolvedValue([]);
  });

  it('renders hero title and clips grid', async () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = false;

    const wrapper = mount(HomeView, {
      global: { stubs },
    });

    await vi.waitFor(() => {
      expect(wrapper.text()).toContain('Game Clips Library');
      expect(wrapper.text()).toContain('First Clip');
    });
  });

  it('does not render admin visibility filter bar when unauthenticated', async () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = false;

    const wrapper = mount(HomeView, {
      global: { stubs },
    });

    await vi.waitFor(() => {
      expect(wrapper.find('.admin-vis-bar').exists()).toBe(false);
    });
  });

  it('renders admin visibility filter pills when authenticated', async () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = true;
    auth.username = 'admin';

    const wrapper = mount(HomeView, {
      global: { stubs },
    });

    await vi.waitFor(() => {
      expect(wrapper.find('.admin-vis-bar').exists()).toBe(true);
      expect(wrapper.text()).toContain('Public');
      expect(wrapper.text()).toContain('Unlisted');
      expect(wrapper.text()).toContain('Private');
    });
  });

  it('filters by visibility when admin clicks unlisted pill', async () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = true;

    const wrapper = mount(HomeView, {
      global: { stubs },
    });

    await vi.waitFor(() => {
      expect(wrapper.find('.admin-vis-bar').exists()).toBe(true);
    });

    const unlistedBtn = wrapper.findAll('.vis-chip').find((btn) => btn.text().includes('Unlisted'));
    expect(unlistedBtn).toBeDefined();

    await unlistedBtn!.trigger('click');

    expect(api.getClips).toHaveBeenCalledWith(
      expect.objectContaining({
        visibility: 'unlisted',
      })
    );
  });
});
