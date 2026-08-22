import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QuotePage } from './QuotePage';
import * as client from '../api/client';

const navigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate };
});

function renderPage() {
  render(
    <MemoryRouter>
      <QuotePage />
    </MemoryRouter>,
  );

  return {
    fetchButton: screen.getByRole('button', { name: 'Get random quote' }),
    signOutButton: screen.getByRole('button', { name: 'Sign out' }),
    v0Radio: screen.getByRole('radio', { name: 'v0 (controllers)' }),
    v1Radio: screen.getByRole('radio', { name: 'v1 (minimal APIs)' }),
  };
}

describe('QuotePage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    client.saveSession({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });
  });

  it('shows the signed in user and the correlation id', () => {
    renderPage();

    expect(screen.getByText('Signed in as jrb')).toBeDefined();
    expect(screen.getByText('corr-1')).toBeDefined();
  });

  it('renders the quote returned by the api', async () => {
    vi.spyOn(client, 'getRandomQuote').mockResolvedValue({
      id: '8',
      text: 'Talk is cheap. Show me the code.',
      author: 'Linus Torvalds',
    });

    const { fetchButton } = renderPage();
    fireEvent.click(fetchButton);

    expect(await screen.findByText('Talk is cheap. Show me the code.')).toBeDefined();
    expect(screen.getByText('— Linus Torvalds')).toBeDefined();
    expect(screen.getByText('Last status: 200')).toBeDefined();
  });

  it('shows an error and no quote when the request fails', async () => {
    vi.spyOn(client, 'getRandomQuote').mockRejectedValue(new Error('Quote request failed (503)'));
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { fetchButton } = renderPage();
    fireEvent.click(fetchButton);

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toBe('Quote request failed (503)'),
    );
    expect(screen.queryByText('Last status: 200')).toBeNull();
  });

  it('falls back to a generic message for a non-error rejection', async () => {
    vi.spyOn(client, 'getRandomQuote').mockRejectedValue('nope');
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { fetchButton } = renderPage();
    fireEvent.click(fetchButton);

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toBe('Failed to load quote'),
    );
  });

  it('clears the session and returns to the login page on sign out', () => {
    const { signOutButton } = renderPage();

    fireEvent.click(signOutButton);

    expect(client.getSession().accessToken).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/');
  });

  it('defaults to the minimal api version', () => {
    const { v1Radio } = renderPage();

    expect((v1Radio as HTMLInputElement).checked).toBe(true);
  });

  it.each(['v0', 'v1'] as const)('requests the quote from %s when selected', async (version) => {
    const spy = vi.spyOn(client, 'getRandomQuote').mockResolvedValue({
      id: '8',
      text: 'Talk is cheap. Show me the code.',
      author: 'Linus Torvalds',
    });

    const page = renderPage();
    fireEvent.click(version === 'v0' ? page.v0Radio : page.v1Radio);
    fireEvent.click(page.fetchButton);

    await waitFor(() => expect(spy).toHaveBeenCalledWith(version));
    expect(await screen.findByText(`Served by: ${version}`)).toBeDefined();
  });

  it('remembers the selected version for the next visit', () => {
    const { v0Radio } = renderPage();

    fireEvent.click(v0Radio);

    expect(client.getApiVersion()).toBe('v0');
  });

  it('does not claim a server when the request fails', async () => {
    vi.spyOn(client, 'getRandomQuote').mockRejectedValue(new Error('Quote request failed (503)'));
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { fetchButton } = renderPage();
    fireEvent.click(fetchButton);

    await waitFor(() => expect(screen.getByRole('alert')).toBeDefined());
    expect(screen.queryByText(/^Served by:/)).toBeNull();
  });
});
