import { describe, it, expect } from 'vitest';
import { mount } from '@vue/test-utils';
import ClipCard from '../ClipCard.vue';
import type { Clip } from '../../api/client';

const routerLinkStub = {
  'router-link': {
    template: '<a><slot /></a>',
  },
};

describe('ClipCard.vue', () => {
  const baseClip: Clip = {
    id: 'testclip1',
    title: 'Epic Quad Kill in Apex',
    description: 'Ranked clutch play',
    originalFileName: 'apex_clutch.mp4',
    fileSizeBytes: 10485760,
    contentType: 'video/mp4',
    durationSeconds: 75.4,
    hasThumbnail: true,
    videoUrl: '/api/clips/testclip1/stream',
    thumbnailUrl: '/api/clips/testclip1/thumbnail',
    createdAtUtc: new Date().toISOString(),
    viewsCount: 42,
    likesCount: 17,
    visibility: 'public',
    collections: [],
  };

  it('renders clip title, duration, and views count', () => {
    const wrapper = mount(ClipCard, {
      props: { clip: baseClip },
      global: {
        stubs: routerLinkStub,
      },
    });

    expect(wrapper.text()).toContain('Epic Quad Kill in Apex');
    expect(wrapper.text()).toContain('42 views');
    expect(wrapper.text()).toContain('1:15'); // 75 seconds -> 1:15
  });

  it('does not display visibility badge for public clips', () => {
    const wrapper = mount(ClipCard, {
      props: { clip: { ...baseClip, visibility: 'public' } },
      global: {
        stubs: routerLinkStub,
      },
    });

    expect(wrapper.find('.visibility-badge').exists()).toBe(false);
  });

  it('displays amber visibility badge for unlisted clips', () => {
    const wrapper = mount(ClipCard, {
      props: { clip: { ...baseClip, visibility: 'unlisted' } },
      global: {
        stubs: routerLinkStub,
      },
    });

    const badge = wrapper.find('.visibility-badge');
    expect(badge.exists()).toBe(true);
    expect(badge.classes()).toContain('vis-unlisted');
    expect(badge.text()).toContain('unlisted');
  });

  it('displays rose visibility badge for private clips', () => {
    const wrapper = mount(ClipCard, {
      props: { clip: { ...baseClip, visibility: 'private' } },
      global: {
        stubs: routerLinkStub,
      },
    });

    const badge = wrapper.find('.visibility-badge');
    expect(badge.exists()).toBe(true);
    expect(badge.classes()).toContain('vis-private');
    expect(badge.text()).toContain('private');
  });

  it('renders collection badges when assigned to collections', () => {
    const wrapper = mount(ClipCard, {
      props: {
        clip: {
          ...baseClip,
          collections: [
            { id: 'col1', name: 'Highlights' },
            { id: 'col2', name: 'Season 25' },
          ],
        },
      },
      global: {
        stubs: routerLinkStub,
      },
    });

    expect(wrapper.text()).toContain('Highlights');
    expect(wrapper.text()).toContain('Season 25');
  });

  it('renders like count button on card', () => {
    const wrapper = mount(ClipCard, {
      props: { clip: baseClip },
      global: {
        stubs: routerLinkStub,
      },
    });

    const likeBtn = wrapper.find('.card-like-btn');
    expect(likeBtn.exists()).toBe(true);
    expect(likeBtn.text()).toContain('17');
  });
});
