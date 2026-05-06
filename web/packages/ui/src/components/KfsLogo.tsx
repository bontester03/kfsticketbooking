// Minimal placeholder mark while we wait for the school's official SVG drop.
// Reproduces the brand feel: forest-green emblem with a gold accent + Arabic + English wordmark.
// Real logos go under /public/logos/ in each app once the school provides them; this is a
// branded fallback so the UI doesn't ship a "missing image" icon.

interface KfsLogoProps {
  variant?: 'full' | 'arabic' | 'english' | 'emblem';
  className?: string;
}

export function KfsLogo({ variant = 'full', className }: KfsLogoProps) {
  return (
    <span className={`inline-flex items-center gap-2 ${className ?? ''}`}>
      <svg viewBox="0 0 40 40" className="h-9 w-9" aria-hidden="true">
        <circle cx="20" cy="20" r="19" fill="#0d3128" />
        <path
          d="M12 14 q 8 0 8 6 t 8 6 v -12 H 12 z"
          fill="#a08b16"
          opacity="0.9"
        />
      </svg>
      {variant !== 'emblem' && (
        <span className="flex flex-col leading-tight">
          {variant !== 'english' && (
            <span className="text-sm font-bold text-kfs-forest font-arabic">مدرسة الفيصلية</span>
          )}
          {variant !== 'arabic' && (
            <span className="text-xs font-semibold uppercase tracking-wider text-kfs-sage-700">
              Al Faisaliah School
            </span>
          )}
        </span>
      )}
    </span>
  );
}
