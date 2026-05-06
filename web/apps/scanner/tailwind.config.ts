import type { Config } from 'tailwindcss';
// @ts-expect-error -- CJS preset
import preset from '@kfs/ui/tailwind-preset';

const config: Config = {
  presets: [preset],
  content: [
    './index.html',
    './src/**/*.{ts,tsx}',
    '../../packages/ui/src/**/*.{ts,tsx}'
  ]
};
export default config;
