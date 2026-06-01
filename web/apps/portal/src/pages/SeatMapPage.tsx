import { useNavigate } from 'react-router-dom';
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, CountdownPill, LoadingPanel, VenueMap } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import type { ApiError } from '@kfs/types';
import { BookingStatus, ParentRole, ZoneGroup, ZoneSide } from '@kfs/types';
import { api } from '../api';

type SeatRef = { group: 'A' | 'B'; side: ZoneSide; rowLabel: string; seatNumber: number };

const SEATMAP_REFETCH_MS = 5_000;

export default function SeatMapPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });

  // Cart lives on the server (so other students see the hold). Poll it so a release/expiry
  // from the background sweeper is reflected promptly.
  const cartQ = useQuery({
    queryKey: ['cart'],
    queryFn: api.cart.get,
    refetchInterval: SEATMAP_REFETCH_MS,
    staleTime: 0
  });

  const bookingsQ = useQuery({ queryKey: ['bookings'], queryFn: api.bookings.list });

  const groupQs = useQueries({
    queries: [
      {
        queryKey: ['seatmap', eventQ.data?.id, ZoneGroup.A],
        queryFn: () => api.events.seatMap(eventQ.data!.id, ZoneGroup.A),
        enabled: !!eventQ.data,
        staleTime: 0,
        // Poll so other students appear as Held/Booked without needing manual refresh.
        refetchInterval: SEATMAP_REFETCH_MS
      },
      {
        queryKey: ['seatmap', eventQ.data?.id, ZoneGroup.B],
        queryFn: () => api.events.seatMap(eventQ.data!.id, ZoneGroup.B),
        enabled: !!eventQ.data,
        staleTime: 0,
        refetchInterval: SEATMAP_REFETCH_MS
      }
    ]
  });
  const [groupAQ, groupBQ] = groupQs;
  const groupA = groupAQ?.data;
  const groupB = groupBQ?.data;

  const confirmed = bookingsQ.data?.find(b => b.status === BookingStatus.Confirmed);
  const motherOfConfirmed = confirmed?.items?.find(i => i.parentRole === ParentRole.Mother);
  const confirmedSeat: SeatRef | null = confirmed && motherOfConfirmed
    ? {
        group: confirmed.groupChosen === ZoneGroup.A ? 'A' : 'B',
        side: ZoneSide.Female,
        rowLabel: motherOfConfirmed.rowLabel,
        seatNumber: motherOfConfirmed.seatNumber
      }
    : null;

  // Be paranoid here: even though api.cart.get() coerces 204 to null, a stray non-object
  // returned from another path (cache hydration, etc.) shouldn't crash the page.
  const cart = cartQ.data && typeof cartQ.data === 'object' ? cartQ.data : null;
  const cartActive = !!cart && cart.status === BookingStatus.Cart;
  const motherOfCart = cart?.items?.find(i => i.parentRole === ParentRole.Mother);
  const cartSeat: SeatRef | null = cartActive && motherOfCart
    ? {
        group: cart!.groupChosen === ZoneGroup.A ? 'A' : 'B',
        side: ZoneSide.Female,
        rowLabel: motherOfCart.rowLabel,
        seatNumber: motherOfCart.seatNumber
      }
    : null;

  // ---- mutations ----

  const select = useMutation({
    mutationFn: (sel: SeatRef) =>
      api.cart.select(
        sel.group === 'A' ? ZoneGroup.A : ZoneGroup.B,
        sel.side,
        sel.rowLabel,
        sel.seatNumber
      ),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['cart'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
    },
    onError: (e: ApiError) => {
      toast.error(e?.code === 'seat_taken' ? t('errors.seatTaken') : (e?.message ?? t('errors.generic')));
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
    }
  });

  const release = useMutation({
    mutationFn: api.cart.release,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['cart'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
    }
  });

  const checkout = useMutation({
    mutationFn: api.cart.checkout,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      void qc.invalidateQueries({ queryKey: ['cart'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
      toast.success(t('confirmation.title'));
      navigate('/bookings');
    },
    onError: (e: ApiError) => {
      toast.error(e?.code === 'cart_expired' ? t('errors.cartExpired') : (e?.message ?? t('errors.generic')));
      void qc.invalidateQueries({ queryKey: ['cart'] });
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

  if (eventQ.isLoading || bookingsQ.isLoading || cartQ.isLoading || !groupA || !groupB) {
    return <LoadingPanel />;
  }

  // ---- view states ----

  if (confirmed) {
    return (
      <div className="grid gap-4">
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
            <div className="flex flex-wrap gap-2">
              <Button variant="accent" onClick={() => api.bookings.downloadAllPdf()
                .catch((e: { message?: string }) => toast.error(e?.message ?? 'Download failed.'))}>
                Download tickets PDF
              </Button>
              <Button variant="secondary" onClick={() => navigate('/bookings')}>{t('myBookings.title')}</Button>
              <Button
                variant="danger"
                loading={cancelBooking.isPending}
                onClick={() => {
                  if (window.confirm(String(t('rebook.cancelConfirm')))) cancelBooking.mutate();
                }}
              >
                {t('rebook.cancelAndPickAgain')}
              </Button>
            </div>
          </div>
        </Card>

        <VenueMap groupA={groupA} groupB={groupB} eventGender={eventQ.data?.gender} confirmedSeat={confirmedSeat} readOnly onSelect={() => undefined} />
      </div>
    );
  }

  if (cartActive && cartSeat && motherOfCart) {
    const expiresAt = motherOfCart.holdExpiresAt;
    return (
      <div className="grid gap-4">
        <Card className="border-l-4 border-kfs-gold">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 className="text-base font-semibold text-kfs-forest">{t('rebook.cartTitle')}</h2>
              <p className="mt-1 text-sm text-kfs-sage-700">
                {t('rebook.confirmDetails', {
                  group: cartSeat.group,
                  row: cartSeat.rowLabel,
                  seat: cartSeat.seatNumber,
                  pair: (eventQ.data?.pairLabel ?? 'mother and father').toLowerCase()
                })}
              </p>
              <p className="mt-1 text-xs text-kfs-sage-700">{t('rebook.cartHint')}</p>
            </div>
            <div className="flex items-center gap-3">
              <CountdownPill
                expiresAt={expiresAt}
                onExpire={() => qc.invalidateQueries({ queryKey: ['cart'] })}
                prefix={String(t('rebook.expiresIn'))}
              />
            </div>
          </div>
          <div className="mt-4 flex gap-2">
            <Button onClick={() => checkout.mutate()} loading={checkout.isPending}>
              {t('rebook.confirmBooking')}
            </Button>
            <Button variant="secondary" onClick={() => release.mutate()} loading={release.isPending} disabled={checkout.isPending}>
              {t('rebook.changeSeat')}
            </Button>
          </div>
        </Card>

        <VenueMap
          groupA={groupA}
          groupB={groupB}
          eventGender={eventQ.data?.gender}
          pendingSelection={cartSeat}
          disabled
          onSelect={() => undefined}
        />
      </div>
    );
  }

  return (
    <div className="grid gap-4">
      <Card>
        <h1 className="text-xl font-semibold text-kfs-forest">{t('seatMap.title')}</h1>
        <p className="mt-1 text-sm text-kfs-sage-700">{t('rebook.pickHint')}</p>
      </Card>

      <VenueMap
        groupA={groupA}
        groupB={groupB}
        eventGender={eventQ.data?.gender}
        disabled={select.isPending}
        onSelect={(args) => {
          // Optimistically reserve on the server — other students immediately see this seat
          // as Held. The cart query refetch then drives the confirm-bar to appear.
          select.mutate({
            group: args.group,
            side: args.side,
            rowLabel: args.rowLabel,
            seatNumber: args.seatNumber
          });
        }}
      />
    </div>
  );
}
