import type { Meta, StoryObj } from '@storybook/react';
import { expect, fn, userEvent, within } from 'storybook/test';
import { Pager } from './Pager';

const meta = {
  title: 'Quotes/Pager',
  component: Pager,
  args: {
    page: 1,
    totalPages: 2,
    totalItems: 8,
    onPrevious: fn(),
    onNext: fn(),
  },
} satisfies Meta<typeof Pager>;

export default meta;
type Story = StoryObj<typeof meta>;

export const FirstPage: Story = {};

export const LastPage: Story = {
  args: { page: 2, totalPages: 2 },
};

export const SinglePage: Story = {
  args: { page: 1, totalPages: 1, totalItems: 3 },
};

export const MovingForward: Story = {
  play: async ({ canvasElement, args }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: 'Next page' }));
    await expect(args.onNext).toHaveBeenCalledOnce();
  },
};
