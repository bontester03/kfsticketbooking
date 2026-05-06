import clsx from 'clsx';

export function Spinner({ className }: { className?: string }) {
  return (
    <span
      role="status"
      aria-label="Loading"
      className={clsx(
        'inline-block h-6 w-6 animate-spin rounded-full border-2 border-kfs-sage-100 border-t-kfs-forest',
        className
      )}
    />
  );
}

export function LoadingPanel({ label }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-12 text-kfs-forest-700">
      <Spinner />
      {label ? <span className="text-sm">{label}</span> : null}
    </div>
  );
}
