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
    // Reset internal state if necessary, but apiClient is a singleton.
  });

  it('should not call refresh if another tab has a valid lock', async () => {
    const mockToken = 'new-token';
    const mockRefreshToken = 'new-refresh-token';
    
    // Simulate another tab holding the lock
    localStorage.setItem('capu_refresh_lock', Date.now().toString());
    localStorage.setItem('refreshToken', 'old-refresh-token');

    // Mock axios.post for the refresh call (should NOT be called)
    const postMock = vi.mocked(axios.post);

    // Trigger a 401 error
    const error401 = {
      config: { url: '/test', headers: {} },
      response: { status: 401, data: {} }
    };

    // This promise will hang because it's waiting for the storage event
    const refreshPromise = apiClient.interceptors.response.handlers[0].rejected(error401);

    // Verify axios.post was NOT called
    expect(postMock).not.toHaveBeenCalled();

    // Simulate the other tab finishing the refresh and updating localStorage
    localStorage.setItem('accessToken', mockToken);
    
    // Manually trigger the storage event
    const storageEvent = new StorageEvent('storage', {
      key: 'accessToken',
      newValue: mockToken
    });
    window.dispatchEvent(storageEvent);

    // Now the promise should resolve
    // Note: Since we mocked the apiClient instance to just return success, 
    // we just check if it was retried.
  });

  it('should acquire lock and call refresh if no lock exists', async () => {
    localStorage.setItem('refreshToken', 'old-refresh-token');
    
    const postMock = vi.mocked(axios.post).mockResolvedValue({
      data: { token: 'new-token', refreshToken: 'new-refresh-token' }
    });

    const error401 = {
      config: { url: '/test', headers: {} },
      response: { status: 401, data: {} }
    };

    await apiClient.interceptors.response.handlers[0].rejected(error401);

    expect(postMock).toHaveBeenCalledWith(
      'http://localhost:5256/api/auth/refresh',
      { refreshToken: 'old-refresh-token' }
    );
    expect(localStorage.getItem('capu_refresh_lock')).toBeNull(); // Should be cleared in finally
  });
});
