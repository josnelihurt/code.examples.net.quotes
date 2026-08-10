import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import App from './App';
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

  it('redirects unknown routes to the login page', () => {
    renderAt('/does-not-exist');

    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeDefined();
  });
});
