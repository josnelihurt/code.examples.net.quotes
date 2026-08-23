import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QuotesListPage } from './QuotesListPage';
import * as client from '../api/client';
import type { QuotePageResponse } from '../api/client';

const navigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate };
});

function page(overrides: Partial<QuotePageResponse> = {}): QuotePageResponse {
  return {
    items: [
      { id: '1', text: 'Simplicity is the ultimate sophistication.', author: 'Leonardo da Vinci' },
      { id: '2', text: 'Talk is cheap. Show me the code.', author: 'Linus Torvalds' },
    ],
    page: 1,
    pageSize: 5,
    totalItems: 8,
    totalPages: 2,
    ...overrides,
  };
}

async function renderLoadedPage() {
  render(
    <MemoryRouter>
      <QuotesListPage />
    </MemoryRouter>,
  );

  // The pager only exists once the first page has loaded.
  await screen.findByText(/Page \d+ of \d+/);

  return {
    previous: screen.getByRole('button', { name: 'Previous page' }),
    next: screen.getByRole('button', { name: 'Next page' }),
  };
}

describe('QuotesListPage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    client.saveSession({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });
  });

  it('loads the first page on mount', async () => {
    const listQuotes = vi.spyOn(client, 'listQuotes').mockResolvedValue(page());

    await renderLoadedPage();

    expect(screen.getByText('Simplicity is the ultimate sophistication.')).toBeDefined();
    expect(screen.getByText('— Leonardo da Vinci')).toBeDefined();
    expect(screen.getByText(/Page 1 of 2/)).toBeDefined();
    expect(listQuotes).toHaveBeenCalledWith({ page: 1, pageSize: 5 }, 'v1');
  });

  it('disables the previous control on the first page', async () => {
    vi.spyOn(client, 'listQuotes').mockResolvedValue(page());

    const { previous } = await renderLoadedPage();

    expect((previous as HTMLButtonElement).disabled).toBe(true);
  });

  it('requests the next page when next is clicked', async () => {
    const listQuotes = vi
      .spyOn(client, 'listQuotes')
      .mockResolvedValueOnce(page())
      .mockResolvedValueOnce(page({ page: 2, items: [{ id: '6', text: 'Make it work, make it right, make it fast.', author: 'Kent Beck' }] }));

    const { next } = await renderLoadedPage();

    fireEvent.click(next);

    await screen.findByText(/Page 2 of 2/);
    expect(listQuotes).toHaveBeenLastCalledWith({ page: 2, pageSize: 5 }, 'v1');
  });

  it('disables the next control on the last page', async () => {
    vi.spyOn(client, 'listQuotes')
      .mockResolvedValueOnce(page())
      .mockResolvedValueOnce(page({ page: 2 }));

    const { next } = await renderLoadedPage();

    fireEvent.click(next);

    await screen.findByText(/Page 2 of 2/);
    expect((next as HTMLButtonElement).disabled).toBe(true);
  });

  it('shows an alert when the catalog fails to load', async () => {
    vi.spyOn(client, 'listQuotes').mockRejectedValue(new Error('Failed to load quotes (503)'));
    vi.spyOn(console, 'error').mockImplementation(() => {});

    render(
      <MemoryRouter>
        <QuotesListPage />
      </MemoryRouter>,
    );

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toBe('Failed to load quotes (503)'),
    );
  });

  it('shows an empty state and no pager when the catalog has no quotes', async () => {
    vi.spyOn(client, 'listQuotes').mockResolvedValue(page({ items: [], totalItems: 0, totalPages: 0 }));

    render(
      <MemoryRouter>
        <QuotesListPage />
      </MemoryRouter>,
    );

    expect(await screen.findByText('The catalog is empty.')).toBeDefined();
    expect(screen.queryByRole('button', { name: 'Next page' })).toBeNull();
  });

  it('refetches from the first page when the version changes', async () => {
    const listQuotes = vi.spyOn(client, 'listQuotes').mockResolvedValue(page());

    await renderLoadedPage();

    fireEvent.click(screen.getByRole('radio', { name: 'v0 (controllers)' }));

    await waitFor(() => expect(listQuotes).toHaveBeenLastCalledWith({ page: 1, pageSize: 5 }, 'v0'));
    expect(await screen.findByText('Served by: v0')).toBeDefined();
  });

  it('clears the session and returns to the login page on sign out', async () => {
    vi.spyOn(client, 'listQuotes').mockResolvedValue(page());

    await renderLoadedPage();
    fireEvent.click(screen.getByRole('button', { name: 'Sign out' }));

    expect(client.getSession().accessToken).toBeNull();
    expect(navigate).toHaveBeenCalledWith('/');
  });
});
