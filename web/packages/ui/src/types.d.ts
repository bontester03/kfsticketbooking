// Vite handles binary imports (`import logo from './foo.jpg'`) as string URLs at runtime.
// TypeScript needs an ambient declaration so it doesn't error on those imports. Apps include
// this via their tsconfig path glob `../../packages/**`.

declare module '*.jpg'  { const src: string; export default src; }
declare module '*.jpeg' { const src: string; export default src; }
declare module '*.png'  { const src: string; export default src; }
declare module '*.svg'  { const src: string; export default src; }
declare module '*.webp' { const src: string; export default src; }
