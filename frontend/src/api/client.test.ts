import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  ApiError,
  DEFAULT_API_VERSION,
  clearSession,
  createQuote,
  getApiVersion,
  getRandomQuote,
  getSession,
  listQuotes,
  login,
  saveSession,
  setApiVersion,
  type LoginResponse,
  type QuotePageResponse,
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
    expect(url).toBe('/api/v1/auth/login');
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

const catalogPage: QuotePageResponse = {
  items: [
    { id: '1', text: 'Simplicity is the ultimate sophistication.', author: 'Leonardo da Vinci' },
    { id: '2', text: 'Talk is cheap. Show me the code.', author: 'Linus Torvalds' },
  ],
  page: 1,
  pageSize: 5,
  totalItems: 8,
  totalPages: 2,
};

describe('listQuotes', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('throws when there is no session', async () => {
    await expect(listQuotes()).rejects.toThrow('Not authenticated');
  });

  it('omits the query string when no paging is requested', async () => {
    saveSession(loginResponse);
    const fetchMock = mockFetch(jsonResponse(catalogPage));

    await listQuotes();

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/quotes');
  });

  it('serializes the requested page and page size', async () => {
    saveSession(loginResponse);
    const fetchMock = mockFetch(jsonResponse(catalogPage));

    await listQuotes({ page: 2, pageSize: 5 });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/quotes?page=2&pageSize=5');
  });

  it('requests the stored version when none is passed', async () => {
    saveSession(loginResponse);
    setApiVersion('v0');
    const fetchMock = mockFetch(jsonResponse(catalogPage));

    await listQuotes({ page: 1 });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v0/quotes?page=1');
  });

  it('returns the parsed page response', async () => {
    saveSession(loginResponse);
    mockFetch(jsonResponse(catalogPage));

    const page = await listQuotes({ page: 1, pageSize: 5 });

    expect(page).toEqual(catalogPage);
  });

  it('surfaces the validation description for a rejected page request', async () => {
    saveSession(loginResponse);
    mockFetch(
      jsonResponse(
        {
          title: 'One or more validation errors occurred.',
          errors: { 'quote.invalid_page_request': ['Page must be at least 1.'] },
          errorCode: 'quote.invalid_page_request',
        },
        400,
      ),
    );

    const failure = await listQuotes({ page: 0 }).catch((err: unknown) => err);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).status).toBe(400);
    expect((failure as ApiError).errorCode).toBe('quote.invalid_page_request');
    expect((failure as ApiError).message).toBe('Page must be at least 1. (400)');
  });
});

describe('createQuote', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('throws when there is no session', async () => {
    await expect(createQuote({ text: 'Some valid quote text.', author: 'Someone' })).rejects.toThrow(
      'Not authenticated',
    );
  });

  it('posts the quote with the bearer token and the stored correlation id', async () => {
    saveSession(loginResponse);
    const fetchMock = mockFetch(
      jsonResponse({ id: '9', text: 'Some valid quote text.', author: 'E2E Suite' }, 201),
    );

    const quote = await createQuote({ text: 'Some valid quote text.', author: 'E2E Suite' });

    expect(quote).toEqual({ id: '9', text: 'Some valid quote text.', author: 'E2E Suite' });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/v1/quotes');
    expect(init?.method).toBe('POST');
    expect(JSON.parse(String(init?.body))).toEqual({ text: 'Some valid quote text.', author: 'E2E Suite' });
    const headers = init?.headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer issued-token');
    expect(headers['X-Correlation-Id']).toBe('corr-1');
  });

  it('requests the stored version when none is passed', async () => {
    saveSession(loginResponse);
    setApiVersion('v0');
    const fetchMock = mockFetch(jsonResponse({ id: '9', text: 'Some valid quote text.', author: 'E2E Suite' }, 201));

    await createQuote({ text: 'Some valid quote text.', author: 'E2E Suite' });

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v0/quotes');
  });

  it('surfaces the problem detail for a rule-breaking text', async () => {
    saveSession(loginResponse);
    mockFetch(
      jsonResponse(
        {
          title: 'One or more validation errors occurred.',
          errors: { 'quote.text_too_short': ['The quote text must be at least 12 characters long.'] },
          errorCode: 'quote.text_too_short',
        },
        400,
      ),
    );

    const failure = await createQuote({ text: 'short', author: 'E2E Suite' }).catch((err: unknown) => err);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).status).toBe(400);
    expect((failure as ApiError).errorCode).toBe('quote.text_too_short');
    expect((failure as ApiError).message).toBe(
      'The quote text must be at least 12 characters long. (400)',
    );
  });

  it('surfaces the detail of a near-duplicate conflict', async () => {
    saveSession(loginResponse);
    mockFetch(
      jsonResponse(
        {
          title: 'Conflict',
          detail: 'A near-identical quote already exists in the catalog.',
          errorCode: 'quote.duplicate_fingerprint',
        },
        409,
      ),
    );

    const failure = await createQuote({ text: 'Talk is cheap! Show me the code.', author: 'E2E Suite' }).catch(
      (err: unknown) => err,
    );

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).status).toBe(409);
    expect((failure as ApiError).errorCode).toBe('quote.duplicate_fingerprint');
    expect((failure as ApiError).message).toBe(
      'A near-identical quote already exists in the catalog. (409)',
    );
  });

  it('falls back to a generic message when the error body is not json', async () => {
    saveSession(loginResponse);
    mockFetch(new Response('', { status: 503 }));

    await expect(createQuote({ text: 'Some valid quote text.', author: 'E2E Suite' })).rejects.toThrow(
      'Failed to publish quote (503)',
    );
  });
});
