import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@kfs/api-client';
import { Button, Card, CountdownPill, EmptyState, LoadingPanel } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import { formatRiyadhDate } from '@kfs/utils';
import { BookingStatus } from '@kfs/types';
import { api } from '../api';

export default function DashboardPage() {
  const { t, i18n } = useTranslation();
  const displayName = useAuthStore((s) => s.displayName);

  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });
  const bookingsQ = useQuery({ queryKey: ['bookings'], queryFn: api.bookings.list });

  if (eventQ.isLoading || bookingsQ.isLoading) return <LoadingPanel />;

  const ev = eventQ.data;
  const active = bookingsQ.data?.find(b =>
    b.status === BookingStatus.Cart ||
    b.status === BookingStatus.Confirmed ||
    b.status === BookingStatus.RebookWindow);

  return (
    <div className="grid gap-6">
      <Card>
        <h1 className="text-xl font-semibold text-kfs-forest">{t('dashboard.welcome', { name: displayName })}</h1>
        {ev ? (
          <p className="mt-1 text-sm text-kfs-sage-700">
            {ev.name} · {formatRiyadhDate(ev.eventDate, i18n.language as 'en' | 'ar')} · {ev.venue}
          </p>
        ) : null}
      </Card>

      {!active && (
        <EmptyState
          title={t('dashboard.noBooking')}
          action={<Link to="/book"><Button>{t('dashboard.bookNow')}</Button></Link>}
        />
      )}

      {active?.status === BookingStatus.Cart && active.items[0] && (
        <Card className="border-kfs-gold-100">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h2 className="font-semibold text-kfs-forest">
                {t('dashboard.cartActive', { remaining: '' })}
              </h2>
              <p className="mt-1 text-sm text-kfs-sage-700">
                {active.items.map(i => i.fullLabel).join(' · ')}
              </p>
            </div>
            <CountdownPill expiresAt={active.items[0].holdExpiresAt} />
          </div>
          <div className="mt-4 flex gap-2">
            <Link to="/cart"><Button>{t('cart.checkout')}</Button></Link>
          </div>
        </Card>
      )}

      {active?.status === BookingStatus.Confirmed && (
        <Card className="border-kfs-forest/30">
          <h2 className="font-semibold text-kfs-forest">{t('dashboard.confirmed')}</h2>
          <p className="mt-1 text-sm text-kfs-sage-700">
            {active.items.map(i => i.fullLabel).join(' · ')}
          </p>
          <div className="mt-4">
            <Link to="/bookings"><Button variant="secondary">{t('dashboard.viewBooking')}</Button></Link>
          </div>
        </Card>
      )}
    </div>
  );
}
