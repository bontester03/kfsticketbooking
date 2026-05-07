import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Card, LoadingPanel, VenueMap } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import type { ApiError, ZoneSide } from '@kfs/types';
import { ZoneGroup } from '@kfs/types';
import { api } from '../api';

type Pending = { group: 'A' | 'B'; side: ZoneSide; rowLabel: string; seatNumber: number } | null;

export default function SeatMapPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });

  // Fetch both groups in parallel — the venue view shows the whole hall.
  const groupQs = useQueries({
    queries: [
      {
        queryKey: ['seatmap', eventQ.data?.id, ZoneGroup.A],
        queryFn: () => api.events.seatMap(eventQ.data!.id, ZoneGroup.A),
        enabled: !!eventQ.data,
        staleTime: 0
      },
      {
        queryKey: ['seatmap', eventQ.data?.id, ZoneGroup.B],
        queryFn: () => api.events.seatMap(eventQ.data!.id, ZoneGroup.B),
        enabled: !!eventQ.data,
        staleTime: 0
      }
    ]
  });
  const [groupAQ, groupBQ] = groupQs;
  const groupA = groupAQ?.data;
  const groupB = groupBQ?.data;

  const [pending, setPending] = useState<Pending>(null);

  const select = useMutation({
    mutationFn: ({ group, side, rowLabel, seatNumber }: NonNullable<Pending>) =>
      api.cart.select(group === 'A' ? ZoneGroup.A : ZoneGroup.B, side, rowLabel, seatNumber),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      void qc.invalidateQueries({ queryKey: ['cart'] });
      navigate('/cart');
    },
    onError: (e: ApiError) => {
      toast.error(e?.code === 'seat_taken' ? t('errors.seatTaken') : (e?.message ?? t('errors.generic')));
      setPending(null);
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
    }
  });

  if (eventQ.isLoading || !groupA || !groupB) return <LoadingPanel />;

  return (
    <div className="grid gap-4">
      <Card>
        <h1 className="text-xl font-semibold text-kfs-forest">{t('seatMap.title')}</h1>
        <p className="mt-1 text-sm text-kfs-sage-700">{t('sideSelect.subtitle')}</p>
      </Card>

      <VenueMap
        groupA={groupA}
        groupB={groupB}
        pendingSelection={pending}
        disabled={select.isPending}
        onSelect={(args) => {
          setPending({ group: args.group, side: args.side, rowLabel: args.rowLabel, seatNumber: args.seatNumber });
          select.mutate(args);
        }}
      />
    </div>
  );
}
