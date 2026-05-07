import logoFull from '../assets/kfs-logo-full.jpg';
import emblem from '../assets/kfs-emblem.jpg';

interface KfsLogoProps {
  /** `full` shows the Arabic + English wordmark with the portrait. `emblem` is portrait only. */
  variant?: 'full' | 'emblem' | 'arabic' | 'english';
  className?: string;
  /** Pixel height of the rendered mark; width auto. Default 36. */
  height?: number;
}

/**
 * Renders the official school mark. The two source images live in @kfs/ui/assets and Vite
 * fingerprints them into each app's bundle automatically. `arabic` and `english` variants
 * map to `full` for backwards compatibility with earlier callers.
 */
export function KfsLogo({ variant = 'full', className, height = 36 }: KfsLogoProps) {
  const src = variant === 'emblem' ? emblem : logoFull;
  const alt = variant === 'emblem' ? 'King Faisal School emblem' : 'King Faisal School';
  return (
    <img
      src={src}
      alt={alt}
      className={className}
      style={{ height, width: 'auto', display: 'inline-block' }}
    />
  );
}
