import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  API_VERSIONS,
  clearSession,
  getApiVersion,
  getRandomQuote,
  getSession,
  setApiVersion,
  type ApiVersion,
  type QuoteResponse,
} from '../api/client';

export function QuotePage() {
  const navigate = useNavigate();
  const session = getSession();
  const [quote, setQuote] = useState<QuoteResponse | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [version, setVersion] = useState<ApiVersion>(getApiVersion);
  const [servedBy, setServedBy] = useState<ApiVersion | null>(null);

  const chooseVersion = (next: ApiVersion) => {
    setVersion(next);
    setApiVersion(next);
  };

  const fetchQuote = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getRandomQuote(version);
      setQuote(data);
      setStatus('200');
      setServedBy(version);
    } catch (err) {
      setQuote(null);
      setStatus(null);
      setServedBy(null);
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

      {/* Same use cases behind both: v0 is MVC controllers, v1 is minimal APIs. */}
      <fieldset className="versions">
        <legend>API version</legend>
        {API_VERSIONS.map((option) => (
          <label key={option} htmlFor={`version-${option}`}>
            <input
              type="radio"
              id={`version-${option}`}
              name="apiVersion"
              value={option}
              checked={version === option}
              onChange={() => chooseVersion(option)}
            />
            {option === 'v0' ? `${option} (controllers)` : `${option} (minimal APIs)`}
          </label>
        ))}
      </fieldset>

      <button type="button" onClick={fetchQuote} disabled={loading}>
        {loading ? 'Loading...' : 'Get random quote'}
      </button>

      {error && <p className="error" role="alert">{error}</p>}
      {status && <p className="muted">Last status: {status}</p>}
      {servedBy && <p className="muted">Served by: {servedBy}</p>}

      {quote && (
        <blockquote className="quote">
          <p>{quote.text}</p>
          <footer>— {quote.author}</footer>
        </blockquote>
      )}
    </section>
  );
}
