import { useQuery } from '@tanstack/react-query';
import { Card, LoadingPanel, EmptyState, VenueMap } from '@kfs/ui';
import { ZoneGroup } from '@kfs/types';
import { api } from '../api';

export default function SeatMapPage() {
  const groupAQ = useQuery({
    queryKey: ['admin', 'seatmap', 'A'],
    queryFn: () => api.admin.seatMap(ZoneGroup.A),
    refetchInterval: 10_000
  });
  const groupBQ = useQuery({
    queryKey: ['admin', 'seatmap', 'B'],
    queryFn: () => api.admin.seatMap(ZoneGroup.B),
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
