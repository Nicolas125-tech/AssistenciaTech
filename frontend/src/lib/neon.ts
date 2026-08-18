import { createClient } from '@neondatabase/neon-js';
import { BetterAuthReactAdapter } from '@neondatabase/neon-js/auth/react/adapters';

import type { ReactBetterAuthClient } from '@neondatabase/auth';

const rawClient = createClient(import.meta.env.VITE_NEON_AUTH_URL, {
  auth: {
    url: import.meta.env.VITE_NEON_AUTH_URL,
    adapter: BetterAuthReactAdapter() as any,
  },
});

export const neon = rawClient as unknown as Omit<typeof rawClient, 'auth'> & {
  auth: ReactBetterAuthClient;
};
