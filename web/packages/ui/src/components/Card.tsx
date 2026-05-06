import clsx from 'clsx';
import type { HTMLAttributes } from 'react';

export function Card({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={clsx('card', className)} {...rest} />;
}

export function Surface({ className, ...rest }: HTMLAttributes<HTMLDivElement>) {
  return <div className={clsx('surface', className)} {...rest} />;
}
