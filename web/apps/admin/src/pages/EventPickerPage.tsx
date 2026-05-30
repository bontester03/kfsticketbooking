import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@kfs/api-client';
import { Button, Card, KfsLogo, LoadingPanel } from '@kfs/ui';
import { EventGender } from '@kfs/types';
import type { EventDto } from '@kfs/types';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

// Post-login landing page — two cards (Boys / Girls). Picking one stores the event in
// the EventContext and routes to /{slug}/dashboard. Subsequent admin pages render under
// that prefix and the axios interceptor injects ?eventId= on every API call.

export default function EventPickerPage() {
  const navigate = useNavigate();
  const setEvent = useEventContext((s) => s.setEvent);
  const clearEvent = useEventContext((s) => s.clear);
  const displayName = useAuthStore((s) => s.displayName);
  const logout = useAuthStore((s) => s.clear);

  // Clear any stale event context on landing — admin re-picks each time.
  useEffect(() => { clearEvent(); }, [clearEvent]);

  const { data, isLoading, isError, error, refetch } = useQuery({
    queryKey: ['admin', 'events'],
    queryFn: () => api.admin.event.list()
  });

  if (isLoading) return <LoadingPanel />;

  if (isError) {
    return (
      <div className="grid min-h-screen place-items-center px-4">
        <Card className="w-full max-w-md text-center">
          <p className="mb-2 text-sm font-semibold text-red-700">Couldn't load events.</p>
          <p className="mb-4 text-xs text-kfs-sage-700">
            {(error as { message?: string })?.message ?? 'Unknown error'}
          </p>
          <Button variant="secondary" onClick={() => refetch()}>Retry</Button>
        </Card>
      </div>
    );
  }

  const events = data ?? [];
  const boys = events.find((e) => e.gender === EventGender.Male);
  const girls = events.find((e) => e.gender === EventGender.Female);

  const pick = (ev: EventDto) => {
    setEvent({
      id: ev.id,
      slug: ev.slug,
      name: ev.name,
      pairLabel: ev.pairLabel,
      guestSeatsPerPass: ev.guestSeatsPerPass,
      gender: ev.gender
    });
    navigate(`/${ev.slug}/dashboard`);
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-kfs-forest-50 via-white to-kfs-sage-50">
      <header className="border-b border-kfs-sage-100 bg-white">
        <div className="mx-auto flex max-w-screen-2xl items-center justify-between gap-4 px-4 py-3">
          <div className="flex items-center gap-3">
            <KfsLogo />
            <span className="text-xs font-semibold uppercase tracking-wider text-kfs-sage-700">
              Admin Console
            </span>
          </div>
          <div className="flex items-center gap-3">
            <span className="hidden text-sm text-kfs-sage-700 sm:inline">{displayName}</span>
            <Button variant="secondary" onClick={() => { logout(); navigate('/login', { replace: true }); }}>
              Sign out
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-5xl px-4 py-16">
        <div className="mb-10 text-center">
          <h1 className="text-3xl font-bold text-kfs-forest">Pick an event to manage</h1>
          <p className="mt-2 text-sm text-kfs-sage-700">
            Every admin action — students, passes, reports, scans — is scoped to the event you choose here.
          </p>
        </div>

        <div className="grid gap-6 sm:grid-cols-2">
          <EventCard
            event={boys}
            accentClass="from-blue-50 to-indigo-100 border-blue-300"
            iconBg="bg-blue-100 text-blue-700"
            label="Boys Event"
            sublabel="Father & Mother bookings · 3-seat guest passes"
            onPick={pick}
          />
          <EventCard
            event={girls}
            accentClass="from-pink-50 to-rose-100 border-pink-300"
            iconBg="bg-pink-100 text-pink-700"
            label="Girls Event"
            sublabel="Mother & Grandmother bookings · 5-seat guest passes"
            onPick={pick}
          />
        </div>
      </main>
    </div>
  );
}

interface EventCardProps {
  event: EventDto | undefined;
  accentClass: string;
  iconBg: string;
  label: string;
  sublabel: string;
  onPick: (ev: EventDto) => void;
}

function EventCard({ event, accentClass, iconBg, label, sublabel, onPick }: EventCardProps) {
  if (!event) {
    return (
      <Card className="opacity-60">
        <p className="text-sm font-semibold text-kfs-forest">{label}</p>
        <p className="mt-1 text-xs text-kfs-sage-600">Not seeded yet</p>
      </Card>
    );
  }
  return (
    <button
      type="button"
      onClick={() => onPick(event)}
      className={`group rounded-xl border-2 bg-gradient-to-br ${accentClass} p-6 text-left shadow-sm transition-all hover:-translate-y-0.5 hover:shadow-lg`}
    >
      <div className="mb-4 flex items-center justify-between">
        <span className={`inline-flex h-12 w-12 items-center justify-center rounded-full text-lg font-bold ${iconBg}`}>
          {label[0]}
        </span>
        <span className="rounded-full bg-white/70 px-3 py-1 text-xs font-semibold text-kfs-forest">
          {event.slug.toUpperCase()}
        </span>
      </div>
      <p className="text-lg font-bold text-kfs-forest">{event.name}</p>
      <p className="mt-1 text-xs text-kfs-sage-700">{sublabel}</p>
      <div className="mt-4 flex items-center justify-between text-xs">
        <span className="text-kfs-sage-700">{new Date(event.eventDate).toLocaleDateString()}</span>
        <span className="font-semibold text-kfs-forest group-hover:translate-x-0.5">
          Manage →
        </span>
      </div>
    </button>
  );
}
