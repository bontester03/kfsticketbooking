import clsx from 'clsx';
import type { ButtonHTMLAttributes } from 'react';

export type ButtonVariant = 'primary' | 'secondary' | 'accent' | 'danger' | 'ghost';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  loading?: boolean;
}

export function Button({ variant = 'primary', loading, className, children, disabled, ...rest }: ButtonProps) {
  return (
    <button
      type="button"
      className={clsx(`btn-${variant}`, className)}
      disabled={disabled || loading}
      {...rest}
    >
      {loading ? <span className="me-2 h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" /> : null}
      {children}
    </button>
  );
}
