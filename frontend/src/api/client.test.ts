import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  DEFAULT_API_VERSION,
  clearSession,
  getApiVersion,
  getRandomQuote,
  getSession,
  login,
  saveSession,
  setApiVersion,
  type LoginResponse,
} from './client';

const loginResponse: LoginResponse = {
  accessToken: 'issued-token',
  correlationId: 'corr-1',
  expiresIn: 3600,
  username: 'jrb',
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function mockFetch(response: Response) {
  const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(response);
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
}

describe('session storage', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('starts empty', () => {
    expect(getSession()).toEqual({ accessToken: null, correlationId: null, username: null });
  });

  it('round-trips a login response', () => {
    saveSession(loginResponse);

    expect(getSession()).toEqual({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      username: 'jrb',
    });
  });

  it('clears every key', () => {
    saveSession(loginResponse);

    clearSession();

    expect(getSession()).toEqual({ accessToken: null, correlationId: null, username: null });
  });
});

describe('login', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('posts credentials and stores the session', async () => {
    const fetchMock = mockFetch(jsonResponse(loginResponse));

    const result = await login('jrb', 'supersecret');

    expect(result).toEqual(loginResponse);
    expect(getSession().accessToken).toBe('issued-token');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/auth/login');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toEqual({ username: 'jrb', password: 'supersecret' });
  });

  it('sends a 32 character hex correlation id', async () => {
    const fetchMock = mockFetch(jsonResponse(loginResponse));

    await login('jrb', 'supersecret');

    const headers = fetchMock.mock.calls[0][1]?.headers as Record<string, string>;
    expect(headers['X-Correlation-Id']).toMatch(/^[0-9a-f]{32}$/);
  });

  it('falls back to getRandomValues when randomUUID is unavailable', async () => {
    const original = crypto.randomUUID;
    // @ts-expect-error intentional: exercise the getRandomValues branch
    crypto.randomUUID = undefined;

    try {
      const fetchMock = mockFetch(jsonResponse(loginResponse));
      await login('jrb', 'supersecret');

      const headers = fetchMock.mock.calls[0][1]?.headers as Record<string, string>;
      expect(headers['X-Correlation-Id']).toMatch(/^[0-9a-f]{32}$/);
    } finally {
      crypto.randomUUID = original;
    }
  });

  it('surfaces the ProblemDetails title from the server error', async () => {
    mockFetch(jsonResponse({ title: 'Unauthorized', errorCode: 'auth.invalid_credentials' }, 401));

    await expect(login('jrb', 'wrong')).rejects.toThrow('Unauthorized (401)');
    expect(getSession().accessToken).toBeNull();
  });

  it('falls back to a generic message when the error body is not json', async () => {
    mockFetch(new Response('boom', { status: 500 }));

    await expect(login('jrb', 'supersecret')).rejects.toThrow('Invalid credentials');
  });
});

describe('getRandomQuote', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('throws when there is no session', async () => {
    await expect(getRandomQuote()).rejects.toThrow('Not authenticated');
  });

  it('sends the bearer token and the stored correlation id', async () => {
    saveSession(loginResponse);
    const fetchMock = mockFetch(jsonResponse({ id: '1', text: 'hello', author: 'someone' }));

    const quote = await getRandomQuote();

    expect(quote).toEqual({ id: '1', text: 'hello', author: 'someone' });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/v1/quotes/random');
    const headers = init?.headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer issued-token');
    expect(headers['X-Correlation-Id']).toBe('corr-1');
  });

  it('reports the status code when the request fails', async () => {
    saveSession(loginResponse);
    mockFetch(new Response('', { status: 503 }));

    await expect(getRandomQuote()).rejects.toThrow('Quote request failed (503)');
  });
});

describe('api version selection', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('defaults to the minimal api version', () => {
    expect(getApiVersion()).toBe(DEFAULT_API_VERSION);
    expect(DEFAULT_API_VERSION).toBe('v1');
  });

  it('round-trips the chosen version', () => {
    setApiVersion('v0');

    expect(getApiVersion()).toBe('v0');
  });

  it('falls back to the default when the stored value is not a known version', () => {
    sessionStorage.setItem('apiVersion', 'v99');

    expect(getApiVersion()).toBe(DEFAULT_API_VERSION);
  });

  it('keeps the chosen version across sign out', () => {
    saveSession(loginResponse);
    setApiVersion('v0');

    clearSession();

    expect(getApiVersion()).toBe('v0');
  });

  it.each(['v0', 'v1'] as const)('requests %s when asked for it explicitly', async (version) => {
    saveSession(loginResponse);
    const fetchMock = mockFetch(jsonResponse({ id: '1', text: 'hello', author: 'someone' }));

    await getRandomQuote(version);

    expect(fetchMock.mock.calls[0][0]).toBe(`/api/${version}/quotes/random`);
  });

  it('uses the stored version when none is passed', async () => {
    saveSession(loginResponse);
    setApiVersion('v0');
    const fetchMock = mockFetch(jsonResponse({ id: '1', text: 'hello', author: 'someone' }));

    await getRandomQuote();

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v0/quotes/random');
  });
});
