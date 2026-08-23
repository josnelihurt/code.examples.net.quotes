import type { Meta, StoryObj } from '@storybook/react';
import { ErrorAlert } from './ErrorAlert';

const meta = {
  title: 'Quotes/ErrorAlert',
  component: ErrorAlert,
} satisfies Meta<typeof ErrorAlert>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ValidationProblem: Story = {
  args: {
    message: 'The quote text must be at least 12 characters long. (400)',
  },
};

export const Conflict: Story = {
  args: {
    message: 'A near-identical quote already exists in the catalog. (409)',
  },
};

export const Forbidden: Story = {
  args: {
    message: 'Forbidden (403)',
  },
};
