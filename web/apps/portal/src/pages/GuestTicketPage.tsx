import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, EmptyState, LoadingPanel } from '@kfs/ui';
import type { ApiError, GuestPassDto } from '@kfs/types';
import { api } from '../api';

export default function GuestTicketPage() {
  const qc = useQueryClient();
  // Poll every 8 s so the X/3 admit counter ticks up almost in real time as the gate scans.
  const guestQ = useQuery({
    queryKey: ['guest'],
    queryFn: () => api.guest.get(),
    refetchInterval: 8_000,
    refetchOnWindowFocus: true
  });

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

      {/* Live admit counter: starts 0/3, ticks to 1/3 → 2/3 → 3/3 as the gate scans. */}
      <ScanProgress admitted={pass.admittedCount} total={pass.seatsCount} fullyUsed={pass.fullyUsed} />

      <p className="text-xs text-kfs-sage-600">Show this QR at the Guest gate. Each scan admits one guest. This page refreshes automatically.</p>
    </Card>
  );
}

/**
 * Prominent live admit counter: shows a big "X / N" plus a small caption.
 * - 0/N → grey "Not scanned yet"
 * - <N → green, "K guest(s) admitted · M left"
 * - =N → amber, "All N guests admitted"
 */
function ScanProgress({ admitted, total, fullyUsed }: { admitted: number; total: number; fullyUsed: boolean }) {
  const isZero = admitted === 0;
  const colour = isZero
    ? { bg: 'bg-kfs-sage-50', ring: 'ring-kfs-sage-200', big: 'text-kfs-sage-700', small: 'text-kfs-sage-700' }
    : fullyUsed
      ? { bg: 'bg-amber-100', ring: 'ring-amber-300', big: 'text-amber-800', small: 'text-amber-800' }
      : { bg: 'bg-green-100', ring: 'ring-green-300', big: 'text-green-800', small: 'text-green-800' };

  const caption = isZero
    ? 'Not scanned yet'
    : fullyUsed
      ? `All ${total} guests admitted`
      : `${admitted} guest${admitted === 1 ? '' : 's'} admitted · ${total - admitted} left`;

  return (
    <div className={`flex flex-col items-center gap-1 rounded-xl ${colour.bg} px-8 py-4 ring-1 ${colour.ring}`}>
      <div className={`text-4xl font-extrabold tabular-nums ${colour.big}`} aria-live="polite">
        {admitted}<span className="mx-1 opacity-50">/</span>{total}
      </div>
      <div className={`text-xs font-semibold uppercase tracking-wider ${colour.small}`}>{caption}</div>
    </div>
  );
}
