import clsx from 'clsx';

const ZONE_STYLE: Record<string, string> = {
  'VIP AF': 'bg-kfs-forest text-kfs-forest-50',
  'VIP AM': 'bg-kfs-forest-600 text-kfs-forest-50',
  'VIP BF': 'bg-kfs-sage-500 text-white',
  'VIP BM': 'bg-kfs-sage-700 text-white',
  Guest:   'bg-kfs-gold-100 text-kfs-gold-700',
  Staff:   'bg-kfs-sage-100 text-kfs-sage-700',
  Media:   'bg-kfs-gold text-white',
  VVIP:    'bg-kfs-gold-700 text-white'
};

export function ZoneBadge({ label, className }: { label: string; className?: string }) {
  const cls = ZONE_STYLE[label] ?? 'bg-kfs-sage-100 text-kfs-forest-700';
  return (
    <span className={clsx('inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold', cls, className)}>
      {label}
    </span>
  );
}
