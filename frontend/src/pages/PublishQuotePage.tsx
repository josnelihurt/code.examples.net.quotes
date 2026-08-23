import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  clearSession,
  createQuote,
  getApiVersion,
  getSession,
  setApiVersion,
  type ApiVersion,
  type QuoteResponse,
} from '../api/client';
import { ErrorAlert } from '../components/ErrorAlert';
import { PublishForm } from '../components/PublishForm';
import { QuoteCard } from '../components/QuoteCard';
import { VersionSwitcher } from '../components/VersionSwitcher';

export function PublishQuotePage() {
  const navigate = useNavigate();
  const session = getSession();
  const [version, setVersion] = useState<ApiVersion>(getApiVersion);
  const [text, setText] = useState('');
  const [author, setAuthor] = useState('');
  const [published, setPublished] = useState<QuoteResponse | null>(null);
  const [status, setStatus] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [servedBy, setServedBy] = useState<ApiVersion | null>(null);

  const chooseVersion = (next: ApiVersion) => {
    setVersion(next);
    setApiVersion(next);
  };

  const submit = async () => {
    setLoading(true);
    setError(null);
    try {
      const quote = await createQuote({ text: text.trim(), author: author.trim() }, version);
      setPublished(quote);
      setStatus('201');
      setServedBy(version);
      setText('');
      setAuthor('');
    } catch (err) {
      setPublished(null);
      setStatus(null);
      setServedBy(null);
      setError(err instanceof Error ? err.message : 'Failed to publish quote');
      console.error('Publish request failed', err);
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
          <h1>Publish a quote</h1>
          <p className="muted">Signed in as {session.username}</p>
        </div>
        <button type="button" className="secondary" onClick={signOut}>
          Sign out
        </button>
      </div>

      <p>
        <strong>Correlation ID:</strong> <code>{session.correlationId}</code>
      </p>

      <VersionSwitcher version={version} onChange={chooseVersion} />

      <PublishForm
        text={text}
        author={author}
        loading={loading}
        onTextChange={setText}
        onAuthorChange={setAuthor}
        onSubmit={submit}
      />

      {error && <ErrorAlert message={error} />}
      {status && <p className="muted">Last status: {status}</p>}
      {servedBy && <p className="muted">Served by: {servedBy}</p>}

      {published && (
        <div className="published" role="status">
          <p className="muted">Published to the catalog.</p>
          <QuoteCard quote={published} />
          <Link to="/quotes">Browse the catalog</Link>
        </div>
      )}
    </section>
  );
}
