import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount } from '@vue/test-utils';
import LoginModal from '../LoginModal.vue';
import { api } from '../../api/client';

describe('LoginModal.vue', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders modal with username and password inputs', () => {
    const wrapper = mount(LoginModal);

    expect(wrapper.find('h2').text()).toBe('Admin Login');
    expect(wrapper.find('input#admin-user').exists()).toBe(true);
    expect(wrapper.find('input#admin-pass').exists()).toBe(true);
    expect(wrapper.find('button[type="submit"]').text()).toContain('Sign In');
  });

  it('emits close event when clicking close button', async () => {
    const wrapper = mount(LoginModal);

    const closeBtn = wrapper.find('.close-btn');
    await closeBtn.trigger('click');

    expect(wrapper.emitted('close')).toBeTruthy();
  });

  it('shows error banner when submitted with empty fields', async () => {
    const wrapper = mount(LoginModal);

    await wrapper.find('form').trigger('submit.prevent');

    expect(wrapper.find('.error-banner').exists()).toBe(true);
    expect(wrapper.find('.error-banner').text()).toContain('Please enter both username and password');
  });

  it('calls api.login and emits close upon successful login', async () => {
    vi.spyOn(api, 'login').mockResolvedValueOnce({
      token: 'test-token',
      username: 'admin',
    });

    const wrapper = mount(LoginModal);

    await wrapper.find('input#admin-user').setValue('admin');
    await wrapper.find('input#admin-pass').setValue('adminpassword');
    await wrapper.find('form').trigger('submit.prevent');

    expect(api.login).toHaveBeenCalledWith('admin', 'adminpassword');
    // wait for promise to resolve
    await vi.waitFor(() => {
      expect(wrapper.emitted('close')).toBeTruthy();
    });
  });

  it('displays error message when login fails', async () => {
    vi.spyOn(api, 'login').mockRejectedValueOnce(new Error('Invalid admin credentials'));

    const wrapper = mount(LoginModal);

    await wrapper.find('input#admin-user').setValue('admin');
    await wrapper.find('input#admin-pass').setValue('wrong');
    await wrapper.find('form').trigger('submit.prevent');

    await vi.waitFor(() => {
      expect(wrapper.find('.error-banner').exists()).toBe(true);
      expect(wrapper.find('.error-banner').text()).toBe('Invalid admin credentials');
    });
  });
});
