import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LoginPage } from './LoginPage';
import * as client from '../api/client';

const navigate = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigate };
});

function renderPage() {
  render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );

  return {
    username: screen.getByLabelText('Username') as HTMLInputElement,
    password: screen.getByLabelText('Password') as HTMLInputElement,
    submit: screen.getByRole('button', { name: 'Sign in' }),
  };
}

function typeCredentials(username: HTMLInputElement, password: HTMLInputElement) {
  fireEvent.change(username, { target: { value: 'jrb' } });
  fireEvent.change(password, { target: { value: 'supersecret' } });
}

describe('LoginPage', () => {
  it('starts with empty credentials', () => {
    const { username, password } = renderPage();

    expect(username.value).toBe('');
    expect(password.value).toBe('');
  });

  it('sends whatever the user typed', async () => {
    const login = vi.spyOn(client, 'login').mockResolvedValue({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'someone',
    });

    const { username, password, submit } = renderPage();
    fireEvent.change(username, { target: { value: 'someone' } });
    fireEvent.change(password, { target: { value: 'else' } });
    fireEvent.click(submit);

    await waitFor(() => expect(login).toHaveBeenCalledWith('someone', 'else'));
  });

  it('navigates to the quote page after a successful login', async () => {
    vi.spyOn(client, 'login').mockResolvedValue({
      accessToken: 'issued-token',
      correlationId: 'corr-1',
      expiresIn: 3600,
      username: 'jrb',
    });

    const { username, password, submit } = renderPage();
    typeCredentials(username, password);
    fireEvent.click(submit);

    await waitFor(() => expect(navigate).toHaveBeenCalledWith('/quote'));
  });

  it('shows the error message when login fails', async () => {
    vi.spyOn(client, 'login').mockRejectedValue(new Error('Invalid credentials'));
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { username, password, submit } = renderPage();
    typeCredentials(username, password);
    fireEvent.click(submit);

    await waitFor(() => expect(screen.getByRole('alert').textContent).toBe('Invalid credentials'));
    expect(navigate).not.toHaveBeenCalledWith('/quote');
  });

  it('falls back to a generic message for a non-error rejection', async () => {
    vi.spyOn(client, 'login').mockRejectedValue('nope');
    vi.spyOn(console, 'error').mockImplementation(() => {});

    const { username, password, submit } = renderPage();
    typeCredentials(username, password);
    fireEvent.click(submit);

    await waitFor(() => expect(screen.getByRole('alert').textContent).toBe('Login failed'));
  });
});
