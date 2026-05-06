import type { ReactNode } from 'react';

interface EmptyStateProps {
  title: string;
  description?: string;
  action?: ReactNode;
}

export function EmptyState({ title, description, action }: EmptyStateProps) {
  return (
    <div className="surface flex flex-col items-center gap-3 px-8 py-12 text-center">
      <h2 className="text-lg font-semibold text-kfs-forest">{title}</h2>
      {description ? <p className="max-w-prose text-sm text-kfs-sage-700">{description}</p> : null}
      {action}
    </div>
  );
}
