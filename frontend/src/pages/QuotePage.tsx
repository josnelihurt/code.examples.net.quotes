import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { clearSession, getRandomQuote, getSession, type QuoteResponse } from '../api/client';

export function QuotePage() {
  const navigate = useNavigate();
  const session = getSession();
  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchQuote = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getRandomQuote();
      setQuote(data);
      setStatus('200');
    } catch (err) {
      setQuote(null);
      setStatus(null);
      setError(err instanceof Error ? err.message : 'Failed to load quote');
      console.error('Quote request failed', err);
    } finally {
      setLoading(false);
    }
  };

  const signOut = () => {
    clearSession();
    navigate('/');
  };

  return (
    <section className="panel">
      <div className="row">
        <div>
          <h1>Random quote</h1>
          <p className="muted">Signed in as {session.username}</p>
        </div>
        <button type="button" className="secondary" onClick={signOut}>
          Sign out
        </button>
      </div>

      <p>
        <strong>Correlation ID:</strong> <code>{session.correlationId}</code>
      </p>

      <button type="button" onClick={fetchQuote} disabled={loading}>
        {loading ? 'Loading...' : 'Get random quote'}
      </button>

      {error && <p className="error" role="alert">{error}</p>}
      {status && <p className="muted">Last status: {status}</p>}

      {quote && (
        <blockquote className="quote">
          <p>{quote.text}</p>
          <footer>— {quote.author}</footer>
        </blockquote>
      )}
    </section>
  );
}
