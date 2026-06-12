import { describe, it, expect, vi, beforeEach } from 'vitest';
import * as Sentry from '@sentry/react';
import React from 'react';
import { render, screen } from '@testing-library/react';
import ErrorBoundary from '../core/components/ErrorBoundary';
import apiClient from '../core/api/apiClient';
import axios from 'axios';

// Mock Sentry
vi.mock('@sentry/react', () => ({
  captureException: vi.fn(),
  captureMessage: vi.fn(),
  browserTracingIntegration: vi.fn(),
  replayIntegration: vi.fn(),
  init: vi.fn(),
}));

// Mock axios for apiClient tests
vi.mock('axios', async () => {
  const actual = await vi.importActual('axios');
  return {
    default: {
      ...actual.default,
      create: vi.fn(() => {
        const instance = vi.fn();
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

describe('Observability Integration', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    window.history.pushState({}, 'Test', '/dashboard');
  });

  describe('ErrorBoundary', () => {
    it('should capture exceptions and report to Sentry', () => {
      const ThrowError = () => {
        throw new Error('Test Error');
      };

      // Suppress console.error for the expected error
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      render(
        <ErrorBoundary>
          <ThrowError />
        </ErrorBoundary>
      );

      expect(Sentry.captureException).toHaveBeenCalled();
      const [error, context] = vi.mocked(Sentry.captureException).mock.calls[0];
      expect(error.message).toBe('Test Error');
      expect(context).toHaveProperty('extra');
      
      consoleSpy.mockRestore();
    });
  });

  describe('apiClient', () => {
    it('should report session_revoked to Sentry', async () => {
      const error401 = {
        config: { url: '/test', headers: {} },
        response: { 
          status: 401, 
          data: { reason: 'session_revoked' } 
        }
      };

      try {
        await apiClient.interceptors.response.handlers[0].rejected(error401);
      } catch (e) {
        // Expected to reject
      }

      expect(Sentry.captureMessage).toHaveBeenCalledWith(
        'Session revoked by server',
        expect.objectContaining({ level: 'info' })
      );
    });

    it('should report missing refresh token to Sentry', async () => {
      localStorage.removeItem('refreshToken');
      
      const error401 = {
        config: { url: '/test', headers: {} },
        response: { status: 401, data: {} }
      };

      try {
        await apiClient.interceptors.response.handlers[0].rejected(error401);
      } catch (e) {
        // Expected
      }

      expect(Sentry.captureMessage).toHaveBeenCalledWith(
        'Refresh token missing from storage',
        expect.objectContaining({ level: 'warning' })
      );
    });

    it('should report refresh failure to Sentry', async () => {
      localStorage.setItem('refreshToken', 'some-token');
      vi.mocked(axios.post).mockRejectedValue(new Error('Network Error'));

      const error401 = {
        config: { url: '/test', headers: {} },
        response: { status: 401, data: {} }
      };

      try {
        await apiClient.interceptors.response.handlers[0].rejected(error401);
      } catch (e) {
        // Expected
      }

      expect(Sentry.captureException).toHaveBeenCalledWith(
        expect.any(Error),
        expect.objectContaining({ tags: { type: 'auth_refresh_failure' } })
      );
    });
  });
});
