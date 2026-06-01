import { useQuery } from '@tanstack/react-query';
import { Card, LoadingPanel, EmptyState, VenueMap } from '@kfs/ui';
import { ZoneGroup } from '@kfs/types';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

export default function SeatMapPage() {
  const eventId = useEventContext((s) => s.eventId);
  const groupAQ = useQuery({
    queryKey: ['admin', 'seatmap', eventId, 'A'],
    queryFn: () => api.admin.seatMap(ZoneGroup.A),
    enabled: !!eventId,
    refetchInterval: 10_000
  });
  const groupBQ = useQuery({
    queryKey: ['admin', 'seatmap', eventId, 'B'],
    queryFn: () => api.admin.seatMap(ZoneGroup.B),
    enabled: !!eventId,
    refetchInterval: 10_000
  });

  const loading = groupAQ.isLoading || groupBQ.isLoading;
  const ready = groupAQ.data && groupBQ.data;

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Live seat map</h1>
        <p className="text-sm text-kfs-sage-700">Real-time view of VIP A &amp; B occupancy. Auto-refreshes every 10s.</p>
      </div>

      {loading ? (
        <LoadingPanel label="Loading seat map…" />
      ) : !ready ? (
        <EmptyState title="Seat map unavailable" description="Couldn't load the venue layout. Try again shortly." />
      ) : (
        <Card className="overflow-x-auto">
          <VenueMap
            groupA={groupAQ.data!}
            groupB={groupBQ.data!}
            readOnly
            onSelect={() => { /* read-only in admin */ }}
          />
        </Card>
      )}
    </div>
  );
}
