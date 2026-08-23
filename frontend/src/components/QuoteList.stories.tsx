import type { Meta, StoryObj } from '@storybook/react';
import { QuoteList } from './QuoteList';

const meta = {
  title: 'Quotes/QuoteList',
  component: QuoteList,
} satisfies Meta<typeof QuoteList>;

export default meta;
type Story = StoryObj<typeof meta>;

const seeded = [
  { id: '1', text: 'Simplicity is the ultimate sophistication.', author: 'Leonardo da Vinci' },
  { id: '2', text: 'Code is like humor. When you have to explain it, it\'s bad.', author: 'Cory House' },
  { id: '3', text: 'First, solve the problem. Then, write the code.', author: 'John Johnson' },
];

export const OnePage: Story = {
  args: { quotes: seeded },
};

export const EmptyCatalog: Story = {
  args: { quotes: [] },
};
