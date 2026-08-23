import type { Preview } from '@storybook/react';
// The components style themselves with the app's global sheet, so stories render as
// they do inside the SPA.
import '../src/index.css';
import '../src/App.css';

const preview: Preview = {
  parameters: {
    layout: 'centered',
  },
};

export default preview;
