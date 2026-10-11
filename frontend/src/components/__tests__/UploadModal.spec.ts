import { describe, it, expect, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import UploadModal from '../UploadModal.vue';
import { api } from '../../api/client';

describe('UploadModal.vue', () => {
  it('renders upload modal with file drop area and visibility options', () => {
    vi.spyOn(api, 'getCollections').mockResolvedValue([]);
    const wrapper = mount(UploadModal);

    expect(wrapper.find('h2').text()).toBe('Upload Video Game Clip');
    expect(wrapper.find('.dropzone').exists()).toBe(true);

    // Visibility radio options
    const radios = wrapper.findAll('input[type="radio"]');
    expect(radios.length).toBe(3);
    expect(wrapper.text()).toContain('Public');
    expect(wrapper.text()).toContain('Unlisted');
    expect(wrapper.text()).toContain('Private');
  });

  it('defaults visibility to public', () => {
    vi.spyOn(api, 'getCollections').mockResolvedValue([]);
    const wrapper = mount(UploadModal);

    const publicRadio = wrapper.find<HTMLInputElement>('input[value="public"]');
    expect(publicRadio.element.checked).toBe(true);
  });

  it('updates selected visibility when radio button is clicked', async () => {
    vi.spyOn(api, 'getCollections').mockResolvedValue([]);
    const wrapper = mount(UploadModal);

    const unlistedRadio = wrapper.find<HTMLInputElement>('input[value="unlisted"]');
    await unlistedRadio.setValue();

    expect(unlistedRadio.element.checked).toBe(true);
  });

  it('shows error banner when form is submitted without selecting a file', async () => {
    vi.spyOn(api, 'getCollections').mockResolvedValue([]);
    const wrapper = mount(UploadModal);

    await wrapper.find('form').trigger('submit.prevent');

    expect(wrapper.find('.error-banner').exists()).toBe(true);
    expect(wrapper.find('.error-banner').text()).toContain('Please select a video file');
  });

  it('emits close event when close button is clicked', async () => {
    vi.spyOn(api, 'getCollections').mockResolvedValue([]);
    const wrapper = mount(UploadModal);

    await wrapper.find('.close-btn').trigger('click');
    expect(wrapper.emitted('close')).toBeTruthy();
  });
});
