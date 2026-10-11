import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';
import AiAnalysisModal from '../AiAnalysisModal.vue';
import { api, type Clip } from '../../api/client';

vi.mock('../../api/client', () => ({
  api: {
    analyzeClipAi: vi.fn(),
    updateClip: vi.fn(),
  },
}));

vi.mock('../../stores/toast', () => ({
  useToast: () => ({
    success: vi.fn(),
    error: vi.fn(),
  }),
}));

describe('AiAnalysisModal.vue', () => {
  const sampleClip: Clip = {
    id: 'testclip1',
    title: 'Epic Quad Kill Clip',
    description: 'Sniper quad kill on Haven',
    originalFileName: 'clip.mp4',
    fileSizeBytes: 1024000,
    contentType: 'video/mp4',
    durationSeconds: 45.0,
    hasThumbnail: true,
    videoUrl: '/api/clips/testclip1/stream',
    thumbnailUrl: '/api/clips/testclip1/thumbnail',
    createdAtUtc: '2026-10-11T12:00:00Z',
    viewsCount: 15,
    visibility: 'public',
    tags: ['sniper', 'haven'],
    collections: [],
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders modal when show is true', () => {
    const wrapper = mount(AiAnalysisModal, {
      props: {
        show: true,
        clip: sampleClip,
      },
    });

    expect(wrapper.text()).toContain('AI Clip Analysis with Gemini');
    expect(wrapper.text()).toContain('Both (Tags & Sub-clips)');
    expect(wrapper.text()).toContain('Tags Only');
    expect(wrapper.text()).toContain('Sub-clips Only');
    expect(wrapper.text()).toContain('#sniper');
    expect(wrapper.text()).toContain('#haven');
  });

  it('does not render when show is false', () => {
    const wrapper = mount(AiAnalysisModal, {
      props: {
        show: false,
        clip: sampleClip,
      },
    });

    expect(wrapper.find('.modal-backdrop').exists()).toBe(false);
  });

  it('calls api.analyzeClipAi when clicking Analyze Clip', async () => {
    vi.mocked(api.analyzeClipAi).mockResolvedValue({
      tags: ['clutch', 'ace'],
      subClips: [
        {
          title: 'Clean Quickscope',
          description: 'Instant headshot',
          startSeconds: 5.0,
          endSeconds: 15.0,
        },
      ],
      summary: 'Player hits four consecutive sniper shots to secure the round.',
    });

    const wrapper = mount(AiAnalysisModal, {
      props: {
        show: true,
        clip: sampleClip,
      },
    });

    const runBtn = wrapper.find('.btn-run-ai');
    await runBtn.trigger('click');

    expect(api.analyzeClipAi).toHaveBeenCalledWith('testclip1', 'both');

    // Wait for promise resolution
    await wrapper.vm.$nextTick();
    await wrapper.vm.$nextTick();

    expect(wrapper.text()).toContain('Clean Quickscope');
    expect(wrapper.text()).toContain('#clutch');
    expect(wrapper.text()).toContain('#ace');
    expect(wrapper.text()).toContain('Player hits four consecutive sniper shots');
  });

  it('emits openTrimWithSuggestion when clicking Open in Cut Tool', async () => {
    vi.mocked(api.analyzeClipAi).mockResolvedValue({
      tags: [],
      subClips: [
        {
          title: 'Highlight Ace',
          description: 'Winning moment',
          startSeconds: 10.0,
          endSeconds: 22.0,
        },
      ],
      summary: null,
    });

    const wrapper = mount(AiAnalysisModal, {
      props: {
        show: true,
        clip: sampleClip,
      },
    });

    await wrapper.find('.btn-run-ai').trigger('click');
    await wrapper.vm.$nextTick();
    await wrapper.vm.$nextTick();

    const cutBtn = wrapper.find('.btn-cut-now');
    expect(cutBtn.exists()).toBe(true);
    await cutBtn.trigger('click');

    expect(wrapper.emitted('openTrimWithSuggestion')).toBeTruthy();
    expect(wrapper.emitted('openTrimWithSuggestion')![0][0]).toEqual({
      title: 'Highlight Ace',
      description: 'Winning moment',
      startSeconds: 10.0,
      endSeconds: 22.0,
    });
  });

  it('saves updated tags via api.updateClip', async () => {
    vi.mocked(api.updateClip).mockResolvedValue({
      ...sampleClip,
      tags: ['sniper', 'haven', 'clutch'],
    });

    const wrapper = mount(AiAnalysisModal, {
      props: {
        show: true,
        clip: sampleClip,
      },
    });

    // Add custom tag
    const input = wrapper.find('.custom-tag-input');
    await input.setValue('clutch');
    await input.trigger('keydown.enter');

    expect(wrapper.text()).toContain('#clutch');

    const saveBtn = wrapper.find('.btn-save-tags');
    await saveBtn.trigger('click');

    expect(api.updateClip).toHaveBeenCalledWith('testclip1', {
      title: 'Epic Quad Kill Clip',
      description: 'Sniper quad kill on Haven',
      visibility: 'public',
      tags: ['sniper', 'haven', 'clutch'],
    });
    expect(wrapper.emitted('tagsUpdated')).toBeTruthy();
  });
});
