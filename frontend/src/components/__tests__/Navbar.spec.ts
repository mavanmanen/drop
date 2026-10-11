import { describe, it, expect, vi } from 'vitest';
import { mount } from '@vue/test-utils';
import Navbar from '../Navbar.vue';
import { useAuth } from '../../stores/auth';

vi.mock('vue-router', () => ({
  useRouter: () => ({
    currentRoute: { value: { path: '/' } },
    push: vi.fn(),
  }),
}));

const routerLinkStub = {
  'router-link': {
    template: '<a><slot /></a>',
  },
};

describe('Navbar.vue', () => {
  it('renders brand logo and nav links', () => {
    const wrapper = mount(Navbar, {
      global: {
        stubs: routerLinkStub,
      },
    });

    expect(wrapper.text()).toContain('drop');
    expect(wrapper.text()).toContain('Clips');
    expect(wrapper.text()).toContain('Collections');
  });

  it('shows Admin Login button when not authenticated', () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = false;
    auth.username = null;

    const wrapper = mount(Navbar, {
      global: {
        stubs: routerLinkStub,
      },
    });

    const loginBtn = wrapper.find('.btn-login');
    expect(loginBtn.exists()).toBe(true);
    expect(loginBtn.text()).toContain('Admin Login');
    expect(wrapper.find('.btn-upload').exists()).toBe(false);
  });

  it('shows Upload button and user name when authenticated as admin', () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = true;
    auth.username = 'admin';

    const wrapper = mount(Navbar, {
      global: {
        stubs: ['router-link'],
      },
    });

    expect(wrapper.find('.btn-upload').exists()).toBe(true);
    expect(wrapper.find('.user-name').text()).toBe('admin');
    expect(wrapper.find('.btn-login').exists()).toBe(false);
  });

  it('emits open-login event when Admin Login is clicked', async () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = false;

    const wrapper = mount(Navbar, {
      global: {
        stubs: ['router-link'],
      },
    });

    await wrapper.find('.btn-login').trigger('click');
    expect(wrapper.emitted('open-login')).toBeTruthy();
  });

  it('emits open-upload event when Upload button is clicked', async () => {
    const { state: auth } = useAuth();
    auth.isAuthenticated = true;

    const wrapper = mount(Navbar, {
      global: {
        stubs: ['router-link'],
      },
    });

    await wrapper.find('.btn-upload').trigger('click');
    expect(wrapper.emitted('open-upload')).toBeTruthy();
  });

  it('emits search event when user types in search input', async () => {
    const wrapper = mount(Navbar, {
      global: {
        stubs: ['router-link'],
      },
    });

    const searchInput = wrapper.find('input[type="search"]');
    await searchInput.setValue('valorant ace');
    await searchInput.trigger('input');

    expect(wrapper.emitted('search')).toBeTruthy();
    expect(wrapper.emitted('search')?.[0]).toEqual(['valorant ace']);
  });
});
