import type { FormEvent } from 'react';

// The input limits mirror the contract metadata; the server stays the single source of
// validation, so nothing beyond the length caps is enforced here.
const TEXT_MAX_LENGTH = 280;
const AUTHOR_MAX_LENGTH = 80;

interface PublishFormProps {
  text: string;
  author: string;
  loading: boolean;
  onTextChange: (text: string) => void;
  onAuthorChange: (author: string) => void;
  onSubmit: () => void;
}

export function PublishForm({
  text,
  author,
  loading,
  onTextChange,
  onAuthorChange,
  onSubmit,
}: Readonly<PublishFormProps>) {
  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    onSubmit();
  };

  return (
    <form onSubmit={handleSubmit} className="form">
      <label htmlFor="publish-text">
        <span>Text</span>
        <textarea
          id="publish-text"
          value={text}
          maxLength={TEXT_MAX_LENGTH}
          onChange={(event) => onTextChange(event.target.value)}
          rows={3}
          required
        />
      </label>
      <label htmlFor="publish-author">
        <span>Author</span>
        <input
          id="publish-author"
          value={author}
          maxLength={AUTHOR_MAX_LENGTH}
          onChange={(event) => onAuthorChange(event.target.value)}
          required
        />
      </label>
      <button type="submit" disabled={loading}>
        {loading ? 'Publishing...' : 'Publish quote'}
      </button>
    </form>
  );
}
