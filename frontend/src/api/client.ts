const TOKEN_KEY = 'accessToken';
const CORRELATION_KEY = 'correlationId';
const USERNAME_KEY = 'username';

export interface LoginResponse {
  accessToken: string;
  correlationId: string;
  expiresIn: number;
  username: string;
}

export interface QuoteResponse {
  id: string;
  text: string;
  author: string;
}

function createCorrelationId(): string {
  // randomUUID is only exposed in secure contexts, so fall back to raw random bytes.
  if (typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID().replace(/-/g, '');
  }

  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
}

export function getSession() {
  return {
    accessToken: sessionStorage.getItem(TOKEN_KEY),
    correlationId: sessionStorage.getItem(CORRELATION_KEY),
    username: sessionStorage.getItem(USERNAME_KEY),
  };
}

export function clearSession() {
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(CORRELATION_KEY);
  sessionStorage.removeItem(USERNAME_KEY);
}

export function saveSession(login: LoginResponse) {
  sessionStorage.setItem(TOKEN_KEY, login.accessToken);
  sessionStorage.setItem(CORRELATION_KEY, login.correlationId);
  sessionStorage.setItem(USERNAME_KEY, login.username);
}

export async function login(username: string, password: string): Promise<LoginResponse> {
  const correlationId = createCorrelationId();
  const response = await fetch('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': correlationId,
    },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    const payload = await response.json().catch(() => ({ error: 'Invalid credentials' }));
    throw new Error(payload.error ?? `Login failed (${response.status})`);
  }

  const data = (await response.json()) as LoginResponse;
  saveSession(data);
  return data;
}

export async function getRandomQuote(): Promise<QuoteResponse> {
  const { accessToken, correlationId } = getSession();
  if (!accessToken || !correlationId) {
    throw new Error('Not authenticated');
  }

  const response = await fetch('/api/v1/quotes/random', {
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'X-Correlation-Id': correlationId,
    },
  });

  if (!response.ok) {
    throw new Error(`Quote request failed (${response.status})`);
  }

  return (await response.json()) as QuoteResponse;
}
