import { useQuery } from '@tanstack/react-query';
import { Card, LoadingPanel, EmptyState } from '@kfs/ui';
import type { ZoneCapacityDto } from '@kfs/types';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

export default function DashboardPage() {
  const eventId = useEventContext((s) => s.eventId);
  const q = useQuery({
    queryKey: ['admin', 'dashboard', eventId],
    queryFn: () => api.admin.dashboard(),
    enabled: !!eventId,
    refetchInterval: 15_000
  });

  if (q.isLoading) return <LoadingPanel label="Loading dashboard…" />;
  if (q.isError || !q.data) {
    return <EmptyState title="Couldn't load the dashboard" description="The API may still be starting. Try again in a moment." />;
  }

  const s = q.data;
  const stats = [
    { label: 'Students', value: s.studentsTotal, hint: `${s.studentsLoggedIn} have signed in` },
    { label: 'In cart',  value: s.cartCount, hint: 'seats held right now' },
    { label: 'Confirmed bookings', value: s.confirmed, hint: 'parent seat pairs issued' },
    { label: 'Cancelled', value: s.cancelled, hint: 'released back to pool' },
    { label: 'Scans today', value: s.scansToday, hint: 'tickets verified at the gate' }
  ];

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Dashboard</h1>
        <p className="text-sm text-kfs-sage-700">Live overview of bookings and zone capacity. Auto-refreshes every 15s.</p>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
        {stats.map((st) => (
          <Card key={st.label} className="flex flex-col gap-1">
            <span className="text-2xl font-bold text-kfs-forest">{st.value}</span>
            <span className="text-sm font-medium text-kfs-forest-700">{st.label}</span>
            <span className="text-xs text-kfs-sage-600">{st.hint}</span>
          </Card>
        ))}
      </div>

      <Card>
        <h2 className="mb-3 text-base font-semibold text-kfs-forest">Zone capacity</h2>
        <div className="flex flex-col gap-3">
          {s.zones.map((z) => <ZoneBar key={z.zone} zone={z} />)}
        </div>
      </Card>
    </div>
  );
}

function ZoneBar({ zone }: { zone: ZoneCapacityDto }) {
  const pct = Math.min(100, Math.round(zone.percentIssued));
  return (
    <div>
      <div className="mb-1 flex items-center justify-between text-sm">
        <span className="font-medium text-kfs-forest-700">{zone.zone}</span>
        <span className="text-kfs-sage-600">{zone.issued} / {zone.capacity} ({pct}%)</span>
      </div>
      <div className="h-2.5 w-full overflow-hidden rounded-full bg-kfs-sage-50">
        <div
          className="h-full rounded-full bg-kfs-forest transition-all"
          style={{ width: `${pct}%` }}
        />
      </div>
    </div>
  );
}
