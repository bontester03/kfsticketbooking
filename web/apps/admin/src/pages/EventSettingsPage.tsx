import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, Input, LoadingPanel, EmptyState } from '@kfs/ui';
import type { ApiError, EventDto, UpdateEventRequest } from '@kfs/types';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

// <input type="datetime-local"> wants "yyyy-MM-ddTHH:mm" in local wall-clock time.
function toLocalInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
function fromLocalInput(local: string): string {
  return new Date(local).toISOString();
}

type FormState = {
  name: string; venue: string; venueAddress: string; mapLink: string;
  eventDate: string; bookingOpensAt: string; bookingClosesAt: string;
  cartHoldMinutes: number; cancellationWindowMinutes: number;
  reminderNoteFromAdmin: string; isActive: boolean;
};

function toForm(e: EventDto): FormState {
  return {
    name: e.name, venue: e.venue, venueAddress: e.venueAddress, mapLink: e.mapLink ?? '',
    eventDate: toLocalInput(e.eventDate),
    bookingOpensAt: toLocalInput(e.bookingOpensAt),
    bookingClosesAt: toLocalInput(e.bookingClosesAt),
    cartHoldMinutes: e.cartHoldMinutes,
    cancellationWindowMinutes: e.cancellationWindowMinutes,
    reminderNoteFromAdmin: e.reminderNoteFromAdmin ?? '',
    isActive: e.isActive
  };
}

export default function EventSettingsPage() {
  const qc = useQueryClient();
  // Resolve the current event from the EventContext (set by the picker / layout).
  // We use getById here rather than getBySlug to keep the query key tied to the
  // already-resolved id so cache invalidation lines up with save mutations.
  const eventId = useEventContext((s) => s.eventId);
  const eventQ = useQuery({
    queryKey: ['admin', 'event', eventId],
    queryFn: () => api.admin.event.get(eventId!),
    enabled: !!eventId
  });
  const [form, setForm] = useState<FormState | null>(null);

  useEffect(() => {
    if (eventQ.data && !form) setForm(toForm(eventQ.data));
  }, [eventQ.data, form]);

  const saveM = useMutation({
    mutationFn: () => {
      const e = eventQ.data!;
      const f = form!;
      const req: UpdateEventRequest = {
        name: f.name, venue: f.venue, venueAddress: f.venueAddress,
        mapLink: f.mapLink || null,
        isActive: f.isActive,
        eventDate: fromLocalInput(f.eventDate),
        bookingOpensAt: fromLocalInput(f.bookingOpensAt),
        bookingClosesAt: fromLocalInput(f.bookingClosesAt),
        cartHoldMinutes: f.cartHoldMinutes,
        cancellationWindowMinutes: f.cancellationWindowMinutes,
        reminderNoteFromAdmin: f.reminderNoteFromAdmin || null
      };
      return api.admin.event.update(e.id, req);
    },
    onSuccess: (e) => {
      toast.success('Event settings saved.');
      qc.setQueryData(['admin', 'event'], e);
    },
    onError: (err) => toast.error((err as unknown as ApiError)?.message ?? 'Save failed.')
  });

  if (eventQ.isLoading || !form) return <LoadingPanel label="Loading event…" />;
  if (eventQ.isError || !eventQ.data) {
    return <EmptyState title="No active event" description="Couldn't load the event. Try again shortly." />;
  }

  const set = <K extends keyof FormState>(k: K, v: FormState[K]) => setForm((f) => f ? { ...f, [k]: v } : f);

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Event settings</h1>
        <p className="text-sm text-kfs-sage-700">Edit event details, booking window and hold timers.</p>
      </div>

      <Card className="grid gap-4 sm:grid-cols-2">
        <Input id="name" label="Event name" value={form.name} onChange={(e) => set('name', e.target.value)} />
        <Input id="venue" label="Venue" value={form.venue} onChange={(e) => set('venue', e.target.value)} />
        <Input id="addr" label="Venue address" value={form.venueAddress} onChange={(e) => set('venueAddress', e.target.value)} />
        <Input id="map" label="Map link" value={form.mapLink} onChange={(e) => set('mapLink', e.target.value)} />

        <div>
          <label className="label" htmlFor="eventDate">Event date &amp; time</label>
          <input id="eventDate" type="datetime-local" className="input" value={form.eventDate}
                 onChange={(e) => set('eventDate', e.target.value)} />
        </div>
        <div className="flex items-end gap-2">
          <label className="flex items-center gap-2 text-sm text-kfs-forest-700">
            <input type="checkbox" checked={form.isActive} onChange={(e) => set('isActive', e.target.checked)} />
            Event is active
          </label>
        </div>

        <div>
          <label className="label" htmlFor="opens">Booking opens</label>
          <input id="opens" type="datetime-local" className="input" value={form.bookingOpensAt}
                 onChange={(e) => set('bookingOpensAt', e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="closes">Booking closes</label>
          <input id="closes" type="datetime-local" className="input" value={form.bookingClosesAt}
                 onChange={(e) => set('bookingClosesAt', e.target.value)} />
        </div>

        <Input id="hold" label="Cart hold (minutes)" type="number" min={1} value={form.cartHoldMinutes}
               onChange={(e) => set('cartHoldMinutes', Number(e.target.value) || 1)} />
        <Input id="cancel" label="Cancellation window (minutes)" type="number" min={0} value={form.cancellationWindowMinutes}
               onChange={(e) => set('cancellationWindowMinutes', Number(e.target.value) || 0)} />

        <div className="sm:col-span-2">
          <label className="label" htmlFor="note">Reminder note from admin</label>
          <textarea id="note" className="input min-h-[80px]" value={form.reminderNoteFromAdmin}
                    onChange={(e) => set('reminderNoteFromAdmin', e.target.value)} />
        </div>
      </Card>

      <div>
        <Button variant="accent" loading={saveM.isPending} onClick={() => saveM.mutate()}>Save changes</Button>
      </div>
    </div>
  );
}
