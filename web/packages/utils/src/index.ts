export const RIYADH_TZ = 'Asia/Riyadh';

/// Format a UTC ISO string in the school's wall-clock time. Locale 'en-GB' for English
/// (24h, dd/MM/yyyy) and 'ar-SA' for Arabic. Per spec 15.6 we keep numerals Western-Arabic
/// in both — we don't want Arabic-Indic digits on tickets (gate scanners expect "A12").
export function formatRiyadhDate(
  iso: string | Date,
  locale: 'en' | 'ar' = 'en',
  options: Intl.DateTimeFormatOptions = { dateStyle: 'medium', timeStyle: 'short' }
): string {
  const d = typeof iso === 'string' ? new Date(iso) : iso;
  const tag = locale === 'ar' ? 'ar-SA-u-nu-latn' : 'en-GB';
  return new Intl.DateTimeFormat(tag, { timeZone: RIYADH_TZ, ...options }).format(d);
}

/// Returns the difference in seconds between an ISO timestamp and now. Negative when in
/// the past. Used for the cart-hold and rebook-window countdowns.
export function secondsUntil(iso: string | null | undefined): number {
  if (!iso) return 0;
  return Math.floor((new Date(iso).getTime() - Date.now()) / 1000);
}

export function formatCountdown(seconds: number): string {
  if (seconds <= 0) return '0:00';
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return `${m}:${s.toString().padStart(2, '0')}`;
}

/// Splits an array into rows of `size`. Used by the seat grid (4 rows × 19 seats).
export function chunk<T>(items: T[], size: number): T[][] {
  const out: T[][] = [];
  for (let i = 0; i < items.length; i += size) out.push(items.slice(i, i + size));
  return out;
}
