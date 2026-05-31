import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, EmptyState, LoadingPanel, TicketCard } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import { useAuthStore } from '@kfs/api-client';
import { BookingStatus, EventGender, ZoneGroup } from '@kfs/types';
import { api } from '../api';

export default function MyBookingsPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const navigate = useNavigate();
  const displayName = useAuthStore((s) => s.displayName) ?? '';
  const studentEmail = useAuthStore((s) => s.email) ?? '';

  const bookingsQ = useQuery({ queryKey: ['bookings'], queryFn: api.bookings.list });
  // Student's event drives the pair label — Boys: Father, Girls: Grandmother.
  const eventQ = useQuery({ queryKey: ['events', 'active'], queryFn: api.events.active });
  const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);

  const cancel = useMutation({
    mutationFn: (id: string) => api.bookings.cancel(id),
    onSuccess: () => {
      // Refresh the booking list AND the seat map (freed seats reappear), then drop the
      // student straight onto the picker. The backend's RebookWindow status keeps the
      // pair held against new takers for the configured cancellation window.
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
      void qc.invalidateQueries({ queryKey: ['cart'] });
      toast.message(t('rebook.cancelledNowPick'));
      navigate('/book');
    }
  });

  const resend = useMutation({
    mutationFn: (id: string) => api.bookings.resendEmails(id),
    onSuccess: () => toast.success('OK')
  });

  if (bookingsQ.isLoading) return <LoadingPanel />;
  const confirmed = bookingsQ.data?.find(b => b.status === BookingStatus.Confirmed);
  if (!confirmed) {
    return (
      <EmptyState
        title={t('dashboard.noBooking')}
        action={<Button onClick={() => navigate('/book')}>{t('dashboard.bookNow')}</Button>}
      />
    );
  }

  const groupLetter: 'A' | 'B' = confirmed.groupChosen === ZoneGroup.A ? 'A' : 'B';
  const isGirls = eventQ.data?.gender === EventGender.Female;
  const secondLabel = isGirls ? 'Grandmother' : 'Father';

  // Girls bookings share a single QR across the Mother + Grandmother seats — only the
  // Mother item carries the QR, so we render ONE card combining both seat labels.
  // Boys keep two cards (one per seat / QR).
  const cardItems = isGirls
    ? confirmed.items.filter((i) => !!i.qrCodeImageUrl)
    : confirmed.items;
  const partnerSeatLabel = isGirls
    ? confirmed.items.find((i) => !i.qrCodeImageUrl)?.fullLabel ?? ''
    : '';

  return (
    <div className="flex flex-col gap-3">
      {/* Heading + action strip on top, then the 2-up compact ticket grid. */}
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-lg font-semibold text-kfs-forest">{t('myBookings.title')}</h1>
        <div className="flex flex-wrap gap-2">
          <Button variant="accent" onClick={() => api.bookings.downloadAllPdf()
            .catch((e: { message?: string }) => toast.error(e?.message ?? 'Download failed.'))}>
            Download tickets PDF
          </Button>
          <Button variant="secondary" onClick={() => resend.mutate(confirmed.id)} loading={resend.isPending}>
            {t('myBookings.resendEmails')}
          </Button>
          <Button variant="danger" onClick={() => setConfirmCancelId(confirmed.id)}>
            {t('myBookings.cancel')}
          </Button>
        </div>
      </div>

      {/* Compact 2-up tickets: each card half-width on desktop with reduced min-height + smaller QR
          so both fit without scrolling on a normal viewport. */}
      <div className="grid grid-cols-1 gap-3 md:grid-cols-2
                      [&_.ticket]:min-h-[14rem]
                      [&_.ticket-stub]:p-4 [&_.ticket-stub]:gap-2
                      [&_.ticket-receipt]:p-4
                      [&_.ticket-value]:text-lg
                      [&_.ticket-category]:h-10 [&_.ticket-category]:w-12 [&_.ticket-category]:text-xl
                      [&_.ticket-qr]:h-24 [&_.ticket-qr]:w-24
                      [&_.ticket-clock]:h-7 [&_.ticket-clock]:w-7">
        {cardItems.map((item) => (
          <TicketCard
            key={item.id}
            item={isGirls
              // Show both seat labels on the girls combined ticket (e.g. "A13 & A14")
              ? { ...item, fullLabel: `${item.fullLabel} & ${partnerSeatLabel}` }
              : item}
            studentName={displayName}
            studentEmail={studentEmail}
            parentLabel={isGirls
              ? 'Mother & Grandmother'
              : (item.parentRole === 0 ? 'Mother' : secondLabel)}
            group={groupLetter}
          />
        ))}
      </div>

      {confirmCancelId && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-black/40 px-4">
          <Card className="w-full max-w-md">
            <h2 className="text-base font-semibold text-kfs-forest">{t('myBookings.cancel')}?</h2>
            <p className="mt-2 text-sm text-kfs-sage-700">
              {t('myBookings.cancelConfirm', { minutes: 10 })}
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <Button variant="secondary" onClick={() => setConfirmCancelId(null)}>
                {t('myBookings.cancelKeep')}
              </Button>
              <Button
                variant="danger"
                loading={cancel.isPending}
                onClick={() => { cancel.mutate(confirmCancelId); setConfirmCancelId(null); }}
              >
                {t('myBookings.cancelProceed')}
              </Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
