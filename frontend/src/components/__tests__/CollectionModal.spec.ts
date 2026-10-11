import { describe, it, expect, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import CollectionModal from '../CollectionModal.vue';
import { api } from '../../api/client';

describe('CollectionModal.vue', () => {
  it('renders modal with name, description, and visibility radio options', () => {
    const wrapper = mount(CollectionModal);

    expect(wrapper.find('h2').text()).toBe('Create New Collection');
    expect(wrapper.find('input#col-name').exists()).toBe(true);
    expect(wrapper.find('textarea#col-desc').exists()).toBe(true);

    const radios = wrapper.findAll('input[type="radio"]');
    expect(radios.length).toBe(3);
  });

  it('defaults visibility to public', () => {
    const wrapper = mount(CollectionModal);

    const publicRadio = wrapper.find<HTMLInputElement>('input[value="public"]');
    expect(publicRadio.element.checked).toBe(true);
  });

  it('shows error banner when name is empty', async () => {
    const wrapper = mount(CollectionModal);

    await wrapper.find('form').trigger('submit.prevent');

    expect(wrapper.find('.error-banner').exists()).toBe(true);
    expect(wrapper.find('.error-banner').text()).toContain('Please provide a collection name');
  });

  it('creates collection and emits saved event upon submit', async () => {
    vi.spyOn(api, 'createCollection').mockResolvedValueOnce({
      id: 'newcol123',
      name: 'Best Snipes',
      description: 'Headshots only',
      createdAtUtc: new Date().toISOString(),
      clipCount: 0,
      coverThumbnailUrl: null,
      visibility: 'unlisted',
      clips: [],
    });

    const wrapper = mount(CollectionModal);

    await wrapper.find('input#col-name').setValue('Best Snipes');
    await wrapper.find('textarea#col-desc').setValue('Headshots only');

    const unlistedRadio = wrapper.find<HTMLInputElement>('input[value="unlisted"]');
    await unlistedRadio.setValue();

    await wrapper.find('form').trigger('submit.prevent');

    expect(api.createCollection).toHaveBeenCalledWith({
      name: 'Best Snipes',
      description: 'Headshots only',
      visibility: 'unlisted',
    });

    await vi.waitFor(() => {
      expect(wrapper.emitted('saved')).toBeTruthy();
    });
  });

  it('populates fields when editing an existing collection', () => {
    const existing = {
      id: 'edit1',
      name: 'Old Name',
      description: 'Old Description',
      createdAtUtc: new Date().toISOString(),
      clipCount: 5,
      coverThumbnailUrl: null,
      visibility: 'private' as const,
      clips: [],
    };

    const wrapper = mount(CollectionModal, {
      props: { collection: existing },
    });

    expect(wrapper.find('h2').text()).toBe('Edit Collection');
    expect((wrapper.find('input#col-name').element as HTMLInputElement).value).toBe('Old Name');
    expect((wrapper.find('textarea#col-desc').element as HTMLTextAreaElement).value).toBe('Old Description');
    expect(wrapper.find<HTMLInputElement>('input[value="private"]').element.checked).toBe(true);
  });
});
