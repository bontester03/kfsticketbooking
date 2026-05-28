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
  const mustChange = useAuthStore((s) => s.mustChangePassword);

  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });
  const bookingsQ = useQuery({ queryKey: ['bookings'], queryFn: api.bookings.list });
  const guestQ = useQuery({ queryKey: ['guest'], queryFn: () => api.guest.get() });

  if (eventQ.isLoading || bookingsQ.isLoading) return <LoadingPanel />;

  const ev = eventQ.data;
  const active = bookingsQ.data?.find(b =>
    b.status === BookingStatus.Cart ||
    b.status === BookingStatus.Confirmed ||
    b.status === BookingStatus.RebookWindow);
  const confirmed = active?.status === BookingStatus.Confirmed ? active : undefined;
  const guestPass = guestQ.data ?? null;

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

      <BookingProcessTracker
        passwordSet={!mustChange}
        parentSeatsBooked={!!confirmed}
        guestBooked={!!guestPass}
        parentEmailsSent={!!confirmed && confirmed.items.every(i => i.emailSent)}
      />

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

// ---------- Booking process tracker (BPMN-style horizontal pipeline) ----------

interface StepProps {
  index: number;
  label: string;
  hint: string;
  done: boolean;
  href?: string;
  cta?: string;
}

function Step({ index, label, hint, done, href, cta }: StepProps) {
  const ring = done ? 'ring-green-500' : 'ring-red-400';
  const bg   = done ? 'bg-green-500'  : 'bg-red-400';
  const Icon = done
    ? (
      <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="white" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
        <polyline points="5 12 10 17 19 7" />
      </svg>
    )
    : (
      <svg viewBox="0 0 24 24" className="h-7 w-7" fill="none" stroke="white" strokeWidth="3" strokeLinecap="round">
        <line x1="6" y1="6" x2="18" y2="18" /><line x1="18" y1="6" x2="6" y2="18" />
      </svg>
    );
  return (
    <div className="flex flex-col items-center text-center min-w-[112px] flex-1">
      <div className={`relative grid h-14 w-14 place-items-center rounded-full ${bg} text-white ring-4 ${ring} ring-offset-2 ring-offset-white`}>
        {Icon}
        <span className="absolute -top-1 -right-1 grid h-5 w-5 place-items-center rounded-full bg-white text-[10px] font-bold text-kfs-forest ring-1 ring-kfs-sage-100">
          {index}
        </span>
      </div>
      <div className="mt-2 text-sm font-semibold text-kfs-forest">{label}</div>
      <div className="mt-0.5 text-xs text-kfs-sage-600">{hint}</div>
      {!done && href && cta && (
        <Link to={href} className="mt-2 inline-block rounded-md bg-kfs-forest px-3 py-1 text-xs font-semibold text-white hover:bg-kfs-forest-700">
          {cta}
        </Link>
      )}
    </div>
  );
}

function Connector({ done }: { done: boolean }) {
  // Filled when the *previous* step is done, dashed otherwise — visually shows the journey.
  return (
    <div className="hidden flex-1 items-center px-1 sm:flex">
      <div className={`h-0.5 w-full ${done ? 'bg-green-500' : 'border-t-2 border-dashed border-kfs-sage-200'}`} />
      <svg viewBox="0 0 24 24" className={`-ml-1 h-4 w-4 ${done ? 'text-green-500' : 'text-kfs-sage-400'}`} fill="currentColor">
        <path d="M5 5l10 7-10 7z" />
      </svg>
    </div>
  );
}

interface TrackerProps {
  passwordSet: boolean;
  parentSeatsBooked: boolean;
  guestBooked: boolean;
  parentEmailsSent: boolean;
}

function BookingProcessTracker({ passwordSet, parentSeatsBooked, guestBooked, parentEmailsSent }: TrackerProps) {
  const allDone = passwordSet && parentSeatsBooked && guestBooked;
  return (
    <Card>
      <div className="mb-4 flex items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-kfs-forest">Booking progress</h2>
          <p className="text-xs text-kfs-sage-700">
            <span className="inline-flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full bg-green-500" /> done</span>
            <span className="mx-2 text-kfs-sage-300">·</span>
            <span className="inline-flex items-center gap-1"><span className="inline-block h-2 w-2 rounded-full bg-red-400" /> not yet</span>
          </p>
        </div>
        <span className={`rounded-full px-3 py-1 text-xs font-semibold ${allDone ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'}`}>
          {[passwordSet, parentSeatsBooked, guestBooked].filter(Boolean).length} of 3 steps
        </span>
      </div>
      <div className="flex flex-col items-stretch gap-3 sm:flex-row sm:items-center sm:gap-2">
        <Step index={1} label="Sign in" done={passwordSet}
              hint={passwordSet ? 'Password set' : 'Set a new password'}
              href={passwordSet ? undefined : '/change-password'}
              cta={passwordSet ? undefined : 'Set password'} />
        <Connector done={passwordSet} />
        <Step index={2} label="Parent seats" done={parentSeatsBooked}
              hint={parentSeatsBooked ? 'Mother + Father confirmed' : 'Book a paired VIP seat'}
              href={parentSeatsBooked ? undefined : '/book'}
              cta={parentSeatsBooked ? undefined : 'Book now'} />
        <Connector done={parentSeatsBooked} />
        <Step index={3} label="Guest ticket" done={guestBooked}
              hint={guestBooked ? '1 QR · admits 3' : 'Optional — admits 3 guests'}
              href={guestBooked ? undefined : '/guest'}
              cta={guestBooked ? undefined : 'Book guest'} />
        <Connector done={guestBooked} />
        <Step index={4} label="Ready for the event" done={allDone}
              hint={allDone ? (parentEmailsSent ? 'Tickets emailed' : 'All set') : 'Finish the steps above'} />
      </div>
    </Card>
  );
}
