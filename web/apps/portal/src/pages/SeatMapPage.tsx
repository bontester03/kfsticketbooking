import { useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, LoadingPanel, SeatGrid } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import type { ApiError, SeatMapSeatDto } from '@kfs/types';
import { ZoneGroup, ZoneSide } from '@kfs/types';
import { api } from '../api';

export default function SeatMapPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const qc = useQueryClient();
  const [params] = useSearchParams();
  const groupParam = params.get('group') === 'B' ? 'B' : 'A';
  const groupCode: 1 | 2 = groupParam === 'A' ? ZoneGroup.A : ZoneGroup.B;

  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });
  const seatsQ = useQuery({
    queryKey: ['seatmap', eventQ.data?.id, groupCode],
    queryFn: () => api.events.seatMap(eventQ.data!.id, groupCode),
    enabled: !!eventQ.data,
    staleTime: 0
  });

  const [selected, setSelected] = useState<SeatMapSeatDto | null>(null);

  const select = useMutation({
    mutationFn: ({ seat, side }: { seat: SeatMapSeatDto; side: 1 | 2 }) =>
      api.cart.select(groupCode, side as ZoneSide, seat.rowLabel, seat.seatNumber),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      navigate('/cart');
    },
    onError: (e: ApiError) => {
      toast.error(e?.code === 'seat_taken' ? t('errors.seatTaken') : (e?.message ?? t('errors.generic')));
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
    }
  });

  const onPick = (seat: SeatMapSeatDto, side: 1 | 2) => {
    setSelected(seat);
    select.mutate({ seat, side });
  };

  const mirror = useMemo(() => selected
    ? { row: selected.rowLabel, num: selected.seatNumber }
    : null, [selected]);

  if (eventQ.isLoading || seatsQ.isLoading || !seatsQ.data) return <LoadingPanel />;

  return (
    <div className="grid gap-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold text-kfs-forest">{t('seatMap.title')} — Group {groupParam}</h1>
          <p className="mt-1 text-sm text-kfs-sage-700">{t('sideSelect.subtitle')}</p>
        </div>
        <Button variant="secondary" onClick={() => navigate('/book/group')}>↺</Button>
      </header>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <SeatGrid
            seats={seatsQ.data.femaleZone.seats}
            side={ZoneSide.Female}
            selectedSeatId={selected?.id}
            onSelect={(s) => onPick(s, ZoneSide.Female)}
          />
        </Card>
        <Card>
          <SeatGrid
            seats={seatsQ.data.maleZone.seats}
            side={ZoneSide.Male}
            mirrorRow={mirror?.row}
            mirrorSeatNumber={mirror?.num}
            onSelect={(s) => onPick(s, ZoneSide.Male)}
          />
        </Card>
      </div>

      <div className="flex items-center gap-4 text-xs text-kfs-sage-700">
        <span className="flex items-center gap-1.5"><span className="seat-base seat-available !h-4 !w-4 !text-[0px]" />{t('seatMap.available')}</span>
        <span className="flex items-center gap-1.5"><span className="seat-base seat-held !h-4 !w-4 !text-[0px]" />{t('seatMap.held')}</span>
        <span className="flex items-center gap-1.5"><span className="seat-base seat-booked !h-4 !w-4 !text-[0px]" />{t('seatMap.booked')}</span>
      </div>
    </div>
  );
}
