import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuthStore } from '@kfs/api-client';
import { Button, KfsLogo } from '@kfs/ui';
import clsx from 'clsx';

const NAV = [
  { to: '/',          label: 'Dashboard', end: true },
  { to: '/students',  label: 'Students' },
  { to: '/passes',    label: 'Passes' },
  { to: '/guest',     label: 'Guest' },
  { to: '/scans',     label: 'Scans' },
  { to: '/seatmap',   label: 'Seat Map' },
  { to: '/reports',   label: 'Reports' },
  { to: '/reminders', label: 'Reminders' },
  { to: '/event',     label: 'Event' }
];

export function Layout() {
  const navigate = useNavigate();
  const displayName = useAuthStore((s) => s.displayName);
  const clear = useAuthStore((s) => s.clear);

  const onLogout = () => {
    clear();
    navigate('/login', { replace: true });
  };

  return (
    <div className="min-h-screen flex flex-col bg-kfs-forest-50/30">
      <header className="border-b border-kfs-sage-100 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
          <div className="flex items-center gap-3">
            <KfsLogo href="/" />
            <span className="hidden text-xs font-semibold uppercase tracking-wider text-kfs-sage-700 sm:inline">
              Admin Console
            </span>
          </div>
          <div className="flex items-center gap-2">
            <span className="hidden text-sm text-kfs-sage-700 sm:inline">{displayName}</span>
            <Button variant="secondary" onClick={onLogout}>Sign out</Button>
          </div>
        </div>
        <nav className="mx-auto flex max-w-6xl flex-wrap gap-1 px-4 pb-2">
          {NAV.map((n) => (
            <NavLink key={n.to} to={n.to} end={n.end} className={({ isActive }) => navCls(isActive)}>
              {n.label}
            </NavLink>
          ))}
        </nav>
      </header>
      <main className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">
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
