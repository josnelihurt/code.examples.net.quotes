import { useState } from 'react';
import type { Meta, StoryObj } from '@storybook/react';
import { expect, fn, userEvent, within } from 'storybook/test';
import { PublishForm } from './PublishForm';

const meta = {
  title: 'Quotes/PublishForm',
  component: PublishForm,
  args: {
    text: '',
    author: '',
    loading: false,
    onTextChange: fn(),
    onAuthorChange: fn(),
    onSubmit: fn(),
  },
} satisfies Meta<typeof PublishForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/** The page keeps the form controlled; the story wires the same state flow to type into. */
function StatefulPublishForm({ onSubmit }: { onSubmit: (text: string, author: string) => void }) {
  const [text, setText] = useState('');
  const [author, setAuthor] = useState('');
  return (
    <PublishForm
      text={text}
      author={author}
      loading={false}
      onTextChange={setText}
      onAuthorChange={setAuthor}
      onSubmit={() => onSubmit(text, author)}
    />
  );
}

export const Empty: Story = {};

export const Filled: Story = {
  args: {
    text: 'Talk is cheap. Show me the code.',
    author: 'Linus Torvalds',
  },
};

export const Publishing: Story = {
  args: {
    text: 'Talk is cheap. Show me the code.',
    author: 'Linus Torvalds',
    loading: true,
  },
};

export const SubmittingAQuote: Story = {
  render: (args) => <StatefulPublishForm onSubmit={args.onSubmit} />,
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.type(canvas.getByLabelText('Text'), 'Make it work, make it right, make it fast.');
    await userEvent.type(canvas.getByLabelText('Author'), 'Kent Beck');
    await userEvent.click(canvas.getByRole('button', { name: 'Publish quote' }));

    await expect(args.onSubmit).toHaveBeenCalledWith(
      'Make it work, make it right, make it fast.',
      'Kent Beck',
    );
  },
};
