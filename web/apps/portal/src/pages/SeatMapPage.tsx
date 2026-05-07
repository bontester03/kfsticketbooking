import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, LoadingPanel, VenueMap } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import type { ApiError } from '@kfs/types';
import { BookingStatus, ParentRole, ZoneGroup, ZoneSide } from '@kfs/types';
import { api } from '../api';

type SeatRef = { group: 'A' | 'B'; side: ZoneSide; rowLabel: string; seatNumber: number };

export default function SeatMapPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });
  const bookingsQ = useQuery({ queryKey: ['bookings'], queryFn: api.bookings.list });

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

  const confirmed = bookingsQ.data?.find(b => b.status === BookingStatus.Confirmed);
  const motherItem = confirmed?.items.find(i => i.parentRole === ParentRole.Mother);
  const confirmedSeat: SeatRef | null = confirmed && motherItem
    ? {
        group: confirmed.groupChosen === ZoneGroup.A ? 'A' : 'B',
        side: ZoneSide.Female,
        rowLabel: motherItem.rowLabel,
        seatNumber: motherItem.seatNumber
      }
    : null;

  const [pending, setPending] = useState<SeatRef | null>(null);

  // Two-step commit: reserve the seat (cart/select) then immediately checkout.
  // The cart's 10-min hold is mostly transient here — if checkout throws we release
  // the cart so seats free up for someone else right away.
  const confirm = useMutation({
    mutationFn: async (sel: SeatRef) => {
      await api.cart.select(
        sel.group === 'A' ? ZoneGroup.A : ZoneGroup.B,
        sel.side,
        sel.rowLabel,
        sel.seatNumber
      );
      try {
        return await api.cart.checkout();
      } catch (e) {
        await api.cart.release().catch(() => undefined);
        throw e;
      }
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      void qc.invalidateQueries({ queryKey: ['cart'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
      toast.success(t('confirmation.title'));
      setPending(null);
      navigate('/bookings');
    },
    onError: (e: ApiError) => {
      toast.error(e?.code === 'seat_taken' ? t('errors.seatTaken') : (e?.message ?? t('errors.generic')));
      setPending(null);
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
    }
  });

  const cancelBooking = useMutation({
    mutationFn: () => api.bookings.cancel(confirmed!.id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
      toast.message(t('rebook.cancelledNowPick'));
    }
  });

  if (eventQ.isLoading || bookingsQ.isLoading || !groupA || !groupB) return <LoadingPanel />;

  return (
    <div className="grid gap-4">
      {confirmed ? (
        <Card className="border-l-4 border-emerald-500">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h1 className="text-lg font-semibold text-kfs-forest">{t('rebook.confirmedTitle')}</h1>
              <p className="mt-1 text-sm text-kfs-sage-700">
                {t('rebook.confirmedSeats', {
                  seats: confirmed.items.map(i => `${i.block} ${i.fullLabel}`).join(' · ')
                })}
              </p>
              <p className="mt-1 text-xs text-kfs-sage-700">{t('rebook.readOnlyHint')}</p>
            </div>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => navigate('/bookings')}>{t('myBookings.title')}</Button>
              <Button
                variant="danger"
                loading={cancelBooking.isPending}
                onClick={() => {
                  if (window.confirm(String(t('rebook.cancelConfirm')))) {
                    cancelBooking.mutate();
                  }
                }}
              >
                {t('rebook.cancelAndPickAgain')}
              </Button>
            </div>
          </div>
        </Card>
      ) : pending ? (
        <Card className="border-l-4 border-kfs-gold">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="text-base font-semibold text-kfs-forest">{t('rebook.confirmTitle')}</h2>
              <p className="mt-1 text-sm text-kfs-sage-700">
                {t('rebook.confirmDetails', {
                  group: pending.group,
                  row: pending.rowLabel,
                  seat: pending.seatNumber
                })}
              </p>
              <p className="mt-1 text-xs text-kfs-sage-700">{t('rebook.confirmHint')}</p>
            </div>
            <div className="flex gap-2">
              <Button variant="secondary" onClick={() => setPending(null)} disabled={confirm.isPending}>
                {t('rebook.changeSeat')}
              </Button>
              <Button onClick={() => confirm.mutate(pending)} loading={confirm.isPending}>
                {t('rebook.confirmBooking')}
              </Button>
            </div>
          </div>
        </Card>
      ) : (
        <Card>
          <h1 className="text-xl font-semibold text-kfs-forest">{t('seatMap.title')}</h1>
          <p className="mt-1 text-sm text-kfs-sage-700">{t('rebook.pickHint')}</p>
        </Card>
      )}

      <VenueMap
        groupA={groupA}
        groupB={groupB}
        pendingSelection={pending}
        confirmedSeat={confirmedSeat}
        readOnly={!!confirmed}
        disabled={confirm.isPending || cancelBooking.isPending}
        onSelect={(args) => {
          if (confirmed) return;
          setPending({ group: args.group, side: args.side, rowLabel: args.rowLabel, seatNumber: args.seatNumber });
        }}
      />
    </div>
  );
}
