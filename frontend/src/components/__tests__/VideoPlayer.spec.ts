import { describe, it, expect, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import VideoPlayer from '../VideoPlayer.vue';

describe('VideoPlayer.vue', () => {
  it('renders video player container and video element', () => {
    const wrapper = mount(VideoPlayer, {
      props: {
        src: '/api/clips/test1/stream',
        poster: '/api/clips/test1/thumbnail',
      },
    });

    const video = wrapper.find('video');
    expect(video.exists()).toBe(true);
    expect(video.attributes('src')).toBe('/api/clips/test1/stream');
    expect(video.attributes('poster')).toBe('/api/clips/test1/thumbnail');
  });

  it('toggles play and pause when clicking on the video element', async () => {
    const wrapper = mount(VideoPlayer, {
      props: {
        src: '/api/clips/test1/stream',
        autoplay: false,
      },
    });

    const videoEl = wrapper.find('video').element;
    const playSpy = vi.spyOn(videoEl, 'play').mockImplementation(() => Promise.resolve());
    const pauseSpy = vi.spyOn(videoEl, 'pause').mockImplementation(() => {});

    // First click -> plays video (paused is true initially in jsdom)
    await wrapper.find('video').trigger('click');
    expect(playSpy).toHaveBeenCalled();

    // Simulate video playing
    Object.defineProperty(videoEl, 'paused', { value: false, configurable: true });
    await wrapper.find('video').trigger('click');
    expect(pauseSpy).toHaveBeenCalled();
  });

  it('toggles play and pause when clicking the controls-overlay background', async () => {
    const wrapper = mount(VideoPlayer, {
      props: {
        src: '/api/clips/test1/stream',
        autoplay: false,
      },
    });

    const videoEl = wrapper.find('video').element;
    const playSpy = vi.spyOn(videoEl, 'play').mockImplementation(() => Promise.resolve());

    const overlay = wrapper.find('.controls-overlay');
    expect(overlay.exists()).toBe(true);

    await overlay.trigger('click');
    expect(playSpy).toHaveBeenCalled();
  });

  it('does NOT toggle play when clicking controls inside controls-bar', async () => {
    const wrapper = mount(VideoPlayer, {
      props: {
        src: '/api/clips/test1/stream',
        autoplay: false,
      },
    });

    const videoEl = wrapper.find('video').element;
    const playSpy = vi.spyOn(videoEl, 'play').mockImplementation(() => Promise.resolve());

    const controlsBar = wrapper.find('.controls-bar');
    expect(controlsBar.exists()).toBe(true);

    await controlsBar.trigger('click');
    // Clicking on the bar itself should stop propagation and not toggle play
    expect(playSpy).not.toHaveBeenCalled();
  });
});
