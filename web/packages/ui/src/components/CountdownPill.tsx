import { useEffect, useState } from 'react';
import clsx from 'clsx';
import { formatCountdown, secondsUntil } from '@kfs/utils';

interface CountdownPillProps {
  expiresAt: string;
  onExpire?: () => void;
  prefix?: string;
}

export function CountdownPill({ expiresAt, onExpire, prefix }: CountdownPillProps) {
  const [seconds, setSeconds] = useState(secondsUntil(expiresAt));

  useEffect(() => {
    const interval = setInterval(() => {
      setSeconds((s) => {
        const next = s - 1;
        if (next <= 0 && onExpire) onExpire();
        return next;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [expiresAt, onExpire]);

  const tone = seconds < 60
    ? 'bg-red-50 text-red-700'
    : 'bg-kfs-gold-100 text-kfs-gold-700';

  return (
    <span className={clsx('inline-flex items-center rounded-md px-2.5 py-1 text-xs font-semibold tabular-nums', tone)}>
      {prefix ? <span className="me-1">{prefix}</span> : null}
      {formatCountdown(Math.max(0, seconds))}
    </span>
  );
}
