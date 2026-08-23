import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  clearSession,
  getApiVersion,
  getSession,
  listQuotes,
  setApiVersion,
  type ApiVersion,
  type QuotePageResponse,
} from '../api/client';
import { ErrorAlert } from '../components/ErrorAlert';
import { Pager } from '../components/Pager';
import { QuoteList } from '../components/QuoteList';
import { VersionSwitcher } from '../components/VersionSwitcher';

// The API's own default page size is 20; the catalog ships with 8 seeded quotes, so the
// browser asks for 5 per page to keep the pager meaningful in the demo topology.
const PAGE_SIZE = 5;

export function QuotesListPage() {
  const navigate = useNavigate();
  const session = getSession();
  const [version, setVersion] = useState<ApiVersion>(getApiVersion);
  const [page, setPage] = useState(1);
  const [data, setData] = useState<QuotePageResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [servedBy, setServedBy] = useState<ApiVersion | null>(null);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      setLoading(true);
      setError(null);
      try {
        const result = await listQuotes({ page, pageSize: PAGE_SIZE }, version);
        if (!cancelled) {
          setData(result);
          setServedBy(version);
        }
      } catch (err) {
        if (!cancelled) {
          setData(null);
          setServedBy(null);
          setError(err instanceof Error ? err.message : 'Failed to load quotes');
          console.error('Catalog request failed', err);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [page, version]);

  const chooseVersion = (next: ApiVersion) => {
    setVersion(next);
    setApiVersion(next);
    setPage(1);
  };

  const signOut = () => {
    clearSession();
    navigate('/');
  };

  return (
    <section className="panel">
      <div className="row">
        <div>
          <h1>Catalog</h1>
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

      {error && <ErrorAlert message={error} />}
      {loading && <p className="muted">Loading...</p>}
      {servedBy && <p className="muted">Served by: {servedBy}</p>}

      {data && <QuoteList quotes={data.items} />}
      {data && data.totalPages > 0 && (
        <Pager
          page={data.page}
          totalPages={data.totalPages}
          totalItems={data.totalItems}
          onPrevious={() => setPage((current) => current - 1)}
          onNext={() => setPage((current) => current + 1)}
        />
      )}
    </section>
  );
}
