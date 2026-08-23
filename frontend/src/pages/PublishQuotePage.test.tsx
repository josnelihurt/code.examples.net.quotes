import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PublishQuotePage } from './PublishQuotePage';
import * as client from '../api/client';
import { ApiError } from '../api/client';

const navigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate };
});

function renderPage() {
  render(
    <MemoryRouter>
      <PublishQuotePage />
    </MemoryRouter>,
  );

  return {
    text: screen.getByLabelText('Text') as HTMLTextAreaElement,
    author: screen.getByLabelText('Author') as HTMLInputElement,
    submit: screen.getByRole('button', { name: 'Publish quote' }),
  };
}

function fillForm(text: HTMLTextAreaElement, author: HTMLInputElement) {
  fireEvent.change(text, { target: { value: 'Make it work, then make it right.' } });
  fireEvent.change(author, { target: { value: 'E2E Suite' } });
}

describe('PublishQuotePage', () => {
  beforeEach(() => {
    sessionStorage.clear();
    client.saveSession({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });
  });

  it('sends the trimmed quote to the selected version', async () => {
    const createQuote = vi.spyOn(client, 'createQuote').mockResolvedValue({
      id: '9',
      text: 'Make it work, then make it right.',
      author: 'E2E Suite',
    });

    const { text, author, submit } = renderPage();
    fireEvent.change(text, { target: { value: '  Make it work, then make it right.  ' } });
    fireEvent.change(author, { target: { value: '  E2E Suite  ' } });
    fireEvent.click(submit);

    await waitFor(() => expect(createQuote).toHaveBeenCalledWith(
      { text: 'Make it work, then make it right.', author: 'E2E Suite' },
      'v1',
    ));
  });

  it('confirms the published quote and clears the form', async () => {
    vi.spyOn(client, 'createQuote').mockResolvedValue({
      id: '9',
      text: 'Make it work, then make it right.',
      author: 'E2E Suite',
    });

    const { text, author, submit } = renderPage();
    fillForm(text, author);
    fireEvent.click(submit);

    expect(await screen.findByText('Published to the catalog.')).toBeDefined();
    expect(screen.getByText('Make it work, then make it right.')).toBeDefined();
    expect(screen.getByText('Last status: 201')).toBeDefined();
    expect(screen.getByText('Served by: v1')).toBeDefined();
    expect(text.value).toBe('');
    expect(author.value).toBe('');
    expect(screen.getByRole('link', { name: 'Browse the catalog' })).toBeDefined();
  });

  it('surfaces the validation problem for rule-breaking text', async () => {
    vi.spyOn(client, 'createQuote').mockRejectedValue(
      new ApiError(400, 'The quote text must be at least 12 characters long. (400)', 'quote.text_too_short'),
    );
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { text, author, submit } = renderPage();
    fireEvent.change(text, { target: { value: 'short' } });
    fireEvent.change(author, { target: { value: 'E2E Suite' } });
    fireEvent.click(submit);

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toBe(
        'The quote text must be at least 12 characters long. (400)',
      ),
    );
    expect(screen.queryByText('Published to the catalog.')).toBeNull();
  });

  it('surfaces a near-duplicate conflict', async () => {
    vi.spyOn(client, 'createQuote').mockRejectedValue(
      new ApiError(409, 'A near-identical quote already exists in the catalog. (409)', 'quote.duplicate_fingerprint'),
    );
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { text, author, submit } = renderPage();
    fillForm(text, author);
    fireEvent.click(submit);

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toContain('(409)'),
    );
  });

  it('surfaces a missing write permission', async () => {
    vi.spyOn(client, 'createQuote').mockRejectedValue(new ApiError(403, 'Forbidden (403)'));
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { text, author, submit } = renderPage();
    fillForm(text, author);
    fireEvent.click(submit);

    await waitFor(() => expect(screen.getByRole('alert').textContent).toBe('Forbidden (403)'));
  });

  it('falls back to a generic message for a non-error rejection', async () => {
    vi.spyOn(client, 'createQuote').mockRejectedValue('nope');
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { text, author, submit } = renderPage();
    fillForm(text, author);
    fireEvent.click(submit);

    await waitFor(() =>
      expect(screen.getByRole('alert').textContent).toBe('Failed to publish quote'),
    );
  });

  it('disables the submit button while publishing', async () => {
    vi.spyOn(client, 'createQuote').mockReturnValue(
      new Promise((resolve) => {
        setTimeout(() => resolve({ id: '9', text: 'Make it work, then make it right.', author: 'E2E Suite' }), 50);
      }),
    );

    const { text, author, submit } = renderPage();
    fillForm(text, author);
    fireEvent.click(submit);

    expect((submit as HTMLButtonElement).disabled).toBe(true);
    expect(submit.textContent).toBe('Publishing...');
    await screen.findByText('Published to the catalog.');
    expect((submit as HTMLButtonElement).disabled).toBe(false);
  });
});
