import { QueryClient } from "@tanstack/react-query";

const client = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30 * 1000,
      gcTime: 5 * 60 * 1000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
    mutations: {
      retry: 0,
    },
  },
});

// Dev aid: lets the browser console / E2E tooling inspect cache state.
if (import.meta.env.DEV) {
  window.__capuQueryClient = client;
}

export const queryClient = client;
