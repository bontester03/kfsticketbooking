import { useEffect } from 'react';
import { NavLink, Outlet, useNavigate, useParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { useAuthStore } from '@kfs/api-client';
import { Button, KfsLogo, LoadingPanel } from '@kfs/ui';
import clsx from 'clsx';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

// Tabs under the per-event header. Paths are RELATIVE to /:eventSlug/.
const NAV = [
  { to: 'dashboard', label: 'Dashboard' },
  { to: 'students',  label: 'Students' },
  { to: 'passes',    label: 'Passes' },
  { to: 'guest',     label: 'Guest' },
  { to: 'scans',     label: 'Scans' },
  { to: 'seatmap',   label: 'Seat Map' },
  { to: 'reports',   label: 'Reports' },
  { to: 'reminders', label: 'Reminders' },
  { to: 'event',     label: 'Event' }
];

export function Layout() {
  const navigate = useNavigate();
  const { eventSlug } = useParams<{ eventSlug: string }>();
  const displayName = useAuthStore((s) => s.displayName);
  const clearAuth = useAuthStore((s) => s.clear);
  const setEvent = useEventContext((s) => s.setEvent);
  const ctxSlug = useEventContext((s) => s.eventSlug);
  const ctxName = useEventContext((s) => s.eventName);

  // Resolve the event from the URL slug — handles fresh-tab navigation to /boys/dashboard
  // when the store is empty, and re-syncs if the admin manually edits the URL.
  const needsLoad = !ctxSlug || ctxSlug !== eventSlug;
  const { data: eventDto, isLoading } = useQuery({
    queryKey: ['admin', 'event-by-slug', eventSlug],
    queryFn: () => api.admin.event.getBySlug(eventSlug!),
    enabled: !!eventSlug && needsLoad
  });

  useEffect(() => {
    if (eventDto) {
      setEvent({
        id: eventDto.id,
        slug: eventDto.slug,
        name: eventDto.name,
        pairLabel: eventDto.pairLabel,
        guestSeatsPerPass: eventDto.guestSeatsPerPass,
        gender: eventDto.gender
      });
    }
  }, [eventDto, setEvent]);

  const onLogout = () => {
    clearAuth();
    navigate('/login', { replace: true });
  };

  // While the store is being hydrated from the URL, show a loader rather than
  // letting child pages fire API calls without an eventId.
  if (needsLoad && isLoading) return <LoadingPanel />;

  return (
    <div className="min-h-screen flex flex-col bg-kfs-forest-50/30">
      <header className="border-b border-kfs-sage-100 bg-white">
        <div className="mx-auto flex max-w-screen-2xl items-center justify-between gap-4 px-4 py-3">
          <div className="flex items-center gap-3">
            <KfsLogo href="/" />
            <span className="hidden text-xs font-semibold uppercase tracking-wider text-kfs-sage-700 sm:inline">
              Admin · {ctxName ?? eventDto?.name ?? eventSlug?.toUpperCase()}
            </span>
            <button
              type="button"
              onClick={() => navigate('/')}
              className="rounded-md border border-kfs-sage-200 px-2 py-1 text-xs text-kfs-sage-700 hover:bg-kfs-sage-50"
              title="Switch event"
            >
              Switch ⇄
            </button>
          </div>
          <div className="flex items-center gap-2">
            <span className="hidden text-sm text-kfs-sage-700 sm:inline">{displayName}</span>
            <Button variant="secondary" onClick={onLogout}>Sign out</Button>
          </div>
        </div>
        <nav className="mx-auto flex max-w-screen-2xl flex-wrap gap-1 px-4 pb-2">
          {NAV.map((n) => (
            <NavLink key={n.to} to={n.to} className={({ isActive }) => navCls(isActive)}>
              {n.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="mx-auto w-full max-w-screen-2xl flex-1 px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}

function navCls(active: boolean) {
  return clsx(
    'inline-flex items-center rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
    active ? 'bg-kfs-forest text-white' : 'text-kfs-forest-700 hover:bg-kfs-sage-50'
  );
}
