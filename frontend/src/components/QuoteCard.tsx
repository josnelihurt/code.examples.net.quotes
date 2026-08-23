import type { QuoteResponse } from '../api/client';

interface QuoteCardProps {
  quote: QuoteResponse;
}

export function QuoteCard({ quote }: Readonly<QuoteCardProps>) {
  return (
    <blockquote className="quote">
      <p>{quote.text}</p>
      <footer>— {quote.author}</footer>
    </blockquote>
  );
}
