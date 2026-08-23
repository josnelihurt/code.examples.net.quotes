import type { QuoteResponse } from '../api/client';
import { QuoteCard } from './QuoteCard';

interface QuoteListProps {
  quotes: QuoteResponse[];
}

export function QuoteList({ quotes }: Readonly<QuoteListProps>) {
  if (quotes.length === 0) {
    return <p className="muted">The catalog is empty.</p>;
  }

  return (
    <div className="quote-list">
      {quotes.map((quote) => (
        <QuoteCard key={quote.id} quote={quote} />
      ))}
    </div>
  );
}
