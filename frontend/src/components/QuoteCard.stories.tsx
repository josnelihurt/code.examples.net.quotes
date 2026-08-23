import type { Meta, StoryObj } from '@storybook/react';
import { QuoteCard } from './QuoteCard';

const meta = {
  title: 'Quotes/QuoteCard',
  component: QuoteCard,
} satisfies Meta<typeof QuoteCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Seeded: Story = {
  args: {
    quote: {
      id: '8',
      text: 'Talk is cheap. Show me the code.',
      author: 'Linus Torvalds',
    },
  },
};

export const Published: Story = {
  args: {
    quote: {
      id: '9',
      text: 'The browser suite publishes words that tick at 1760000000000.',
      author: 'Browser Suite',
    },
  },
};
