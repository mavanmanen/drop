import { describe, it, expect, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import TrimModal from '../TrimModal.vue';
import { api, type Clip } from '../../api/client';

describe('TrimModal.vue', () => {
  const dummyClip: Clip = {
    id: 'longclip123',
    title: 'Match Highlights Full Stream',
    description: 'An entire 30 minute match',
    originalFileName: 'full_match.mp4',
    fileSizeBytes: 52428800,
    contentType: 'video/mp4',
    durationSeconds: 120.0,
    hasThumbnail: true,
    videoUrl: '/api/clips/longclip123/stream',
    thumbnailUrl: '/api/clips/longclip123/thumbnail',
    createdAtUtc: new Date().toISOString(),
    viewsCount: 15,
    visibility: 'public',
    collections: [],
  };

  it('renders cut modal with source clip title and default time values', () => {
    const wrapper = mount(TrimModal, {
      props: {
        show: true,
        sourceClip: dummyClip,
        allCollections: [],
        initialCurrentTime: 10.0,
      },
    });

    expect(wrapper.text()).toContain('Cut Shorter Clip');
    expect((wrapper.find('input[type="text"]').element as HTMLInputElement).value).toContain('Match Highlights Full Stream');

    const numberInputs = wrapper.findAll('input[type="number"]');
    expect(numberInputs.length).toBe(2);
    // Start time starts at initialCurrentTime = 10
    expect((numberInputs[0].element as HTMLInputElement).value).toBe('10');
  });

  it('calculates resulting duration dynamically', async () => {
    const wrapper = mount(TrimModal, {
      props: {
        show: true,
        sourceClip: dummyClip,
        allCollections: [],
        initialCurrentTime: 0,
      },
    });

    const numberInputs = wrapper.findAll('input[type="number"]');
    await numberInputs[0].setValue(5);
    await numberInputs[1].setValue(25);

    expect(wrapper.text()).toContain('20.0s');
  });

  it('submits trim request to api and emits trimmed event', async () => {
    const trimmedClip: Clip = {
      ...dummyClip,
      id: 'shortclip1',
      title: 'Sick Snipe',
      durationSeconds: 15.0,
    };

    const trimSpy = vi.spyOn(api, 'trimClip').mockResolvedValue(trimmedClip);

    const wrapper = mount(TrimModal, {
      props: {
        show: true,
        sourceClip: dummyClip,
        allCollections: [],
        initialCurrentTime: 0,
      },
    });

    const numberInputs = wrapper.findAll('input[type="number"]');
    await numberInputs[0].setValue(10);
    await numberInputs[1].setValue(25);

    const titleInput = wrapper.find('input[type="text"]');
    await titleInput.setValue('Sick Snipe');

    await wrapper.find('form').trigger('submit.prevent');

    expect(trimSpy).toHaveBeenCalledWith('longclip123', expect.objectContaining({
      startSeconds: 10,
      endSeconds: 25,
      title: 'Sick Snipe',
    }));

    // Wait for promise resolution
    await wrapper.vm.$nextTick();
    expect(wrapper.emitted('trimmed')?.[0]).toEqual([trimmedClip]);
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('emits close event when close button is clicked', async () => {
    const wrapper = mount(TrimModal, {
      props: {
        show: true,
        sourceClip: dummyClip,
        allCollections: [],
      },
    });

    await wrapper.find('.btn-close').trigger('click');
    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('renders start and end slider handles on the timeline track', () => {
    const wrapper = mount(TrimModal, {
      props: {
        show: true,
        sourceClip: dummyClip,
        allCollections: [],
      },
    });

    expect(wrapper.find('.trim-handle-start').exists()).toBe(true);
    expect(wrapper.find('.trim-handle-end').exists()).toBe(true);
    expect(wrapper.find('.timeline-active-slice').exists()).toBe(true);
  });
});
