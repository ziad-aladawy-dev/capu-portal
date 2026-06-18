import { describe, it, expect, vi, beforeEach } from 'vitest';
import axios from 'axios';
import apiClient from '../core/api/apiClient';

vi.mock('axios', async () => {
  const actual = await vi.importActual('axios');
  return {
    default: {
      create: vi.fn(() => {
        const instance = vi.fn((config) => {
          // If this is a retry, just resolve
          if (config.headers?.Authorization?.includes('Bearer new-token')) {
            return Promise.resolve({ data: 'success' });
          }
          return instance.interceptors.response.handlers[0].fulfilled({ config });
        });
        instance.interceptors = {
          request: { use: vi.fn(), handlers: [] },
          response: { use: vi.fn((success, error) => {
            instance.interceptors.response.handlers = [{ fulfilled: success, rejected: error }];
          }), handlers: [] }
        };
        instance.defaults = { baseURL: 'http://localhost:5256/api' };
        return instance;
      }),
      post: vi.fn(),
    }
  };
});

describe('apiClient cross-tab sync', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    window.history.pushState({}, 'Test', '/dashboard');
    if (!globalThis.navigator) {
        globalThis.navigator = {};
    }
  });

  it('should not call refresh if another tab has a valid lock (fallback)', async () => {
    const mockToken = 'new-token';
    localStorage.setItem('refreshToken', 'old-refresh-token');

    // Test fallback behavior without navigator.locks
    const oldLocks = globalThis.navigator.locks;
    globalThis.navigator.locks = undefined;
    
    localStorage.setItem('capu_refresh_lock', Date.now().toString());

    const postMock = vi.mocked(axios.post);

    const error401 = {
      config: { url: '/test', headers: { Authorization: "Bearer old-token"} },
      response: { status: 401, data: {} }
    };

    // catch hanging promise
    apiClient.interceptors.response.handlers[0].rejected(error401).catch(() => {});

    expect(postMock).not.toHaveBeenCalled();
    
    globalThis.navigator.locks = oldLocks;
  });

  it('should acquire lock and call refresh if no lock exists', async () => {
    localStorage.setItem('refreshToken', 'old-refresh-token');
    apiClient.setToken('old-token');

    // Create a mock locks implementation that resolves immediately
    globalThis.navigator.locks = {
        request: vi.fn((name, options, callback) => {
            return Promise.resolve(callback());
        })
    };
    
    const postMock = vi.mocked(axios.post).mockResolvedValue({
      data: { token: 'new-token', refreshToken: 'new-refresh-token' }
    });

    const error401 = {
      config: { url: '/test', headers: { Authorization: "Bearer old-token"} },
      response: { status: 401, data: {} }
    };

    // Ignore rejection error that may be thrown by the interceptor retry mock
    await apiClient.interceptors.response.handlers[0].rejected(error401).catch(() => {});

    expect(postMock).toHaveBeenCalledWith(
      'http://localhost:5256/api/auth/refresh',
      { refreshToken: 'old-refresh-token' }
    );
  });
});
