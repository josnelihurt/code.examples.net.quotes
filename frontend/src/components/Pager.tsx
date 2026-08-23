interface PagerProps {
  page: number;
  totalPages: number;
  totalItems: number;
  onPrevious: () => void;
  onNext: () => void;
}

export function Pager({ page, totalPages, totalItems, onPrevious, onNext }: Readonly<PagerProps>) {
  return (
    <nav className="pager" aria-label="Catalog pages">
      <button
        type="button"
        className="secondary"
        onClick={onPrevious}
        disabled={page <= 1}
      >
        Previous page
      </button>
      <span className="muted">
        Page {page} of {totalPages} · {totalItems} quotes
      </span>
      <button
        type="button"
        className="secondary"
        onClick={onNext}
        disabled={page >= totalPages}
      >
        Next page
      </button>
    </nav>
  );
}
