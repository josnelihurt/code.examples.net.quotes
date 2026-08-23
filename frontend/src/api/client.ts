import type { components } from './schema';

const TOKEN_KEY = 'accessToken';
const CORRELATION_KEY = 'correlationId';
const USERNAME_KEY = 'username';
const API_VERSION_KEY = 'apiVersion';

/**
 * The quote API is served twice over the same use cases: v0 by MVC controllers, v1 by minimal
 * APIs. Both answer identically, so the choice is only about which transport to exercise.
 */
export type ApiVersion = 'v0' | 'v1';

export const API_VERSIONS: readonly ApiVersion[] = ['v0', 'v1'];

export const DEFAULT_API_VERSION: ApiVersion = 'v1';

function isApiVersion(value: string | null): value is ApiVersion {
  return value === 'v0' || value === 'v1';
}

export function getApiVersion(): ApiVersion {
  const stored = sessionStorage.getItem(API_VERSION_KEY);
  return isApiVersion(stored) ? stored : DEFAULT_API_VERSION;
}

export function setApiVersion(version: ApiVersion) {
  sessionStorage.setItem(API_VERSION_KEY, version);
}

export interface LoginResponse {
  accessToken: string;
  correlationId: string;
  expiresIn: number;
  username: string;
}

// Contract types come from the frozen OpenAPI document (npm run gen:api) so the client
// cannot drift from the ratified API. The paging numbers are widened to `number | string`
// by the generator; the contract is numeric, so they are narrowed back here.
type QuoteSchemas = components['schemas'];

export type QuoteResponse = QuoteSchemas['QuoteResponseDto'];

export type CreateQuoteRequest = QuoteSchemas['CreateQuoteRequestDto'];

export interface QuotePageResponse {
  items: QuoteResponse[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface ListQuotesQuery {
  page?: number;
  pageSize?: number;
}

/** The failed API call, with whatever the RFC 9457 problem document could explain. */
export class ApiError extends Error {
  readonly status: number;
  readonly errorCode?: string;

  constructor(status: number, message: string, errorCode?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errorCode = errorCode;
  }
}

interface ProblemShape {
  title?: string | null;
  detail?: string | null;
  errorCode?: string;
  // HttpValidationProblemDetails carries the human explanations keyed by error code.
  errors?: Record<string, string[]>;
}

/**
 * Picks the most helpful line out of a problem document: the validation description when
 * the API rejected input rule by rule, otherwise the detail, title or error code.
 */
async function toApiError(response: Response, fallbackReason: string): Promise<ApiError> {
  const problem = await response.json().catch(() => null) as ProblemShape | null;

  const firstValidation = problem?.errors
    ? Object.values(problem.errors)[0]?.[0]
    : undefined;
  const reason =
    firstValidation ?? problem?.detail ?? problem?.title ?? problem?.errorCode ?? fallbackReason;

  return new ApiError(response.status, `${reason} (${response.status})`, problem?.errorCode);
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
  // The chosen version is a debugging preference, not credentials; it survives sign-out.
}

export function saveSession(login: LoginResponse) {
  sessionStorage.setItem(TOKEN_KEY, login.accessToken);
  sessionStorage.setItem(CORRELATION_KEY, login.correlationId);
  sessionStorage.setItem(USERNAME_KEY, login.username);
}

export async function login(username: string, password: string): Promise<LoginResponse> {
  const correlationId = createCorrelationId();
  const response = await fetch('/api/v1/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Correlation-Id': correlationId,
    },
    body: JSON.stringify({ username, password }),
  });

  if (!response.ok) {
    throw await toApiError(response, 'Invalid credentials');
  }

  const data = (await response.json()) as LoginResponse;
  saveSession(data);
  return data;
}

async function authedFetch(path: string, init: RequestInit, fallbackReason: string): Promise<Response> {
  const { accessToken, correlationId } = getSession();
  if (!accessToken || !correlationId) {
    throw new Error('Not authenticated');
  }

  const response = await fetch(path, {
    ...init,
    headers: {
      ...init.headers,
      Authorization: `Bearer ${accessToken}`,
      'X-Correlation-Id': correlationId,
    },
  });

  if (!response.ok) {
    throw await toApiError(response, fallbackReason);
  }

  return response;
}

export async function getRandomQuote(version: ApiVersion = getApiVersion()): Promise<QuoteResponse> {
  const response = await authedFetch(`/api/${version}/quotes/random`, {}, 'Quote request failed');
  return (await response.json()) as QuoteResponse;
}

export async function listQuotes(
  query: ListQuotesQuery = {},
  version: ApiVersion = getApiVersion(),
): Promise<QuotePageResponse> {
  const params = new URLSearchParams();
  if (query.page !== undefined) {
    params.set('page', String(query.page));
  }
  if (query.pageSize !== undefined) {
    params.set('pageSize', String(query.pageSize));
  }
  const suffix = params.size > 0 ? `?${params.toString()}` : '';

  const response = await authedFetch(`/api/${version}/quotes${suffix}`, {}, 'Failed to load quotes');
  return (await response.json()) as QuotePageResponse;
}

export async function createQuote(
  request: CreateQuoteRequest,
  version: ApiVersion = getApiVersion(),
): Promise<QuoteResponse> {
  const response = await authedFetch(
    `/api/${version}/quotes`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    },
    'Failed to publish quote',
  );
  return (await response.json()) as QuoteResponse;
}
