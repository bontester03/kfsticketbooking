import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, EmptyState, LoadingPanel } from '@kfs/ui';
import type { ApiError, GuestPassDto } from '@kfs/types';
import { api } from '../api';

export default function GuestTicketPage() {
  const qc = useQueryClient();
  const guestQ = useQuery({ queryKey: ['guest'], queryFn: () => api.guest.get() });

  const book = useMutation({
    mutationFn: () => api.guest.book(),
    onSuccess: (pass) => {
      qc.setQueryData(['guest'], pass);
      toast.success('Your guest ticket is booked — it admits 3 people.');
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Could not book the guest ticket.')
  });

  if (guestQ.isLoading) return <LoadingPanel label="Loading your guest ticket…" />;

  const pass = guestQ.data;

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Guest ticket</h1>
        <p className="text-sm text-kfs-sage-700">
          One guest ticket per family — a single QR code that admits <strong>3 guests</strong> to the Guest area.
        </p>
      </div>

      {pass ? (
        <GuestTicket pass={pass} />
      ) : (
        <Card className="flex flex-col items-center gap-4 py-10 text-center">
          <EmptyState
            title="You haven't booked a guest ticket yet"
            description="Book one QR code that admits 3 guests. You can show it at the gate on your phone or print it."
          />
          <Button variant="accent" loading={book.isPending} onClick={() => book.mutate()}>
            Book guest ticket (admits 3)
          </Button>
        </Card>
      )}
    </div>
  );
}

function GuestTicket({ pass }: { pass: GuestPassDto }) {
  const remaining = Math.max(0, pass.seatsCount - pass.admittedCount);
  return (
    <Card className="flex flex-col items-center gap-4 text-center">
      <div className="flex w-full items-center justify-between">
        <span className="rounded-md bg-violet-500 px-2.5 py-1 text-sm font-bold text-white">GUEST</span>
        <span className="rounded-md bg-kfs-forest px-3 py-1 text-sm font-bold text-white">{pass.gate}</span>
        <span className="text-xs text-kfs-sage-600">{pass.ticketNumber}</span>
      </div>

      {pass.qrCodeImageUrl ? (
        <img src={pass.qrCodeImageUrl} alt="Guest QR" className="h-48 w-48 rounded ring-1 ring-slate-200" />
      ) : (
        <div className="grid h-48 w-48 place-items-center rounded bg-slate-50 text-xs text-slate-500">QR pending</div>
      )}

      <div className="text-sm text-kfs-forest-700">Admits <strong>{pass.seatsCount}</strong> guests · one scan per guest at the gate.</div>

      {/* Live scan status */}
      {pass.admittedCount === 0 ? (
        <div className="rounded-md bg-kfs-sage-50 px-4 py-2 text-sm text-kfs-forest">Not scanned yet</div>
      ) : pass.fullyUsed ? (
        <div className="rounded-md bg-amber-100 px-4 py-2 text-sm font-medium text-amber-800">
          Fully used — all {pass.seatsCount} guests admitted.
        </div>
      ) : (
        <div className="rounded-md bg-green-100 px-4 py-2 text-sm font-medium text-green-800">
          {pass.admittedCount} of {pass.seatsCount} admitted · {remaining} entr{remaining === 1 ? 'y' : 'ies'} left.
        </div>
      )}

      <p className="text-xs text-kfs-sage-600">Show this QR at the Guest gate. Each scan admits one guest.</p>
    </Card>
  );
}
