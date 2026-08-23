import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import App from './App';
import * as client from './api/client';
import { saveSession } from './api/client';

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

describe('App routing', () => {
  it('shows the login page at the root', () => {
    renderAt('/');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined();
  });

  it('redirects to the login page when the quote route has no session', () => {
    renderAt('/quote');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined();
    expect(screen.queryByRole('heading', { name: 'Random quote' })).toBeNull();
  });

  it('redirects to the login page when the catalog route has no session', () => {
    renderAt('/quotes');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined();
    expect(screen.queryByRole('heading', { name: 'Catalog' })).toBeNull();
  });

  it('redirects to the login page when the publish route has no session', () => {
    renderAt('/publish');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined();
    expect(screen.queryByRole('heading', { name: 'Publish a quote' })).toBeNull();
  });

  it('renders the quote page when a session exists', () => {
    saveSession({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });

    renderAt('/quote');

    expect(screen.getByRole('heading', { name: 'Random quote' })).toBeDefined();
  });

  it('renders the section navigation on every authenticated page', async () => {
    saveSession({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });
    // The catalog page fetches on mount; keep the test on routing concerns only.
    vi.spyOn(client, 'listQuotes').mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 5,
      totalItems: 0,
      totalPages: 0,
    });

    renderAt('/quotes');

    expect(screen.getByRole('heading', { name: 'Catalog' })).toBeDefined();
    expect(screen.getByRole('link', { name: 'Random' })).toBeDefined();
    expect(screen.getByRole('link', { name: 'Browse' }).getAttribute('aria-current')).toBe('page');
    expect(screen.getByRole('link', { name: 'Publish' })).toBeDefined();
  });

  it('renders the publish page when a session exists', () => {
    saveSession({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });

    renderAt('/publish');

    expect(screen.getByRole('heading', { name: 'Publish a quote' })).toBeDefined();
  });

  it('redirects unknown routes to the login page', () => {
    renderAt('/does-not-exist');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined();
  });
});
