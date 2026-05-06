import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, EmptyState, LoadingPanel, TicketCard } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import { useAuthStore } from '@kfs/api-client';
import { BookingStatus, ZoneGroup } from '@kfs/types';
import { api } from '../api';

export default function MyBookingsPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const displayName = useAuthStore((s) => s.displayName) ?? '';

  const bookingsQ = useQuery({ queryKey: ['bookings'], queryFn: api.bookings.list });
  const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);

  const cancel = useMutation({
    mutationFn: (id: string) => api.bookings.cancel(id),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      toast.success(t('myBookings.cancel'));
    }
  });

  const resend = useMutation({
    mutationFn: (id: string) => api.bookings.resendEmails(id),
    onSuccess: () => toast.success('OK')
  });

  if (bookingsQ.isLoading) return <LoadingPanel />;
  const confirmed = bookingsQ.data?.find(b => b.status === BookingStatus.Confirmed);
  if (!confirmed) {
    return <EmptyState title={t('dashboard.noBooking')} />;
  }

  const groupLetter: 'A' | 'B' = confirmed.groupChosen === ZoneGroup.A ? 'A' : 'B';

  return (
    <div className="grid gap-6">
      <h1 className="text-xl font-semibold text-kfs-forest">{t('myBookings.title')}</h1>
      <div className="grid gap-4 sm:grid-cols-2">
        {confirmed.items.map((item) => (
          <TicketCard
            key={item.id}
            item={item}
            studentName={displayName}
            parentLabel={item.parentRole === 0 ? 'Mother' : 'Father'}
            group={groupLetter}
          />
        ))}
      </div>

      <Card>
        <div className="flex flex-wrap gap-3">
          <Button variant="secondary" onClick={() => resend.mutate(confirmed.id)} loading={resend.isPending}>
            {t('myBookings.resendEmails')}
          </Button>
          <Button variant="danger" onClick={() => setConfirmCancelId(confirmed.id)}>
            {t('myBookings.cancel')}
          </Button>
        </div>
      </Card>

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
              <Button variant="danger" loading={cancel.isPending}
                      onClick={() => { cancel.mutate(confirmCancelId); setConfirmCancelId(null); }}>
                {t('myBookings.cancelProceed')}
              </Button>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
