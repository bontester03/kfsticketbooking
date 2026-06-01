import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, Input, LoadingPanel, EmptyState } from '@kfs/ui';
import type { ApiError } from '@kfs/types';
import { formatRiyadhDate } from '@kfs/utils';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

export default function RemindersPage() {
  const qc = useQueryClient();
  const eventId = useEventContext((s) => s.eventId);
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');

  const logsQ = useQuery({
    queryKey: ['admin', 'reminderLogs', eventId],
    queryFn: () => api.admin.reminders.logs(100),
    enabled: !!eventId
  });

  const sendM = useMutation({
    mutationFn: () => api.admin.reminders.sendUnbooked(subject || undefined, body || undefined),
    onSuccess: () => {
      toast.success('Reminder emails queued to unbooked students.');
      qc.invalidateQueries({ queryKey: ['admin', 'reminderLogs'] });
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Send failed.')
  });

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Reminders</h1>
        <p className="text-sm text-kfs-sage-700">Email students who haven't booked yet. Leave the fields blank to use the default template.</p>
      </div>

      <Card className="flex flex-col gap-3">
        <Input id="subject" label="Custom subject (optional)" value={subject}
               onChange={(e) => setSubject(e.target.value)} placeholder="Reminder: book your seats for the KFS event" />
        <div>
          <label className="label" htmlFor="body">Custom message (optional)</label>
          <textarea id="body" className="input min-h-[120px]" value={body}
                    onChange={(e) => setBody(e.target.value)}
                    placeholder="Dear parent, please log in and reserve your seats before the deadline…" />
        </div>
        <div>
          <Button variant="accent" loading={sendM.isPending} onClick={() => sendM.mutate()}>
            Send to unbooked students
          </Button>
        </div>
      </Card>

      <h2 className="text-base font-semibold text-kfs-forest">Recent reminder log</h2>
      {logsQ.isLoading ? (
        <LoadingPanel label="Loading log…" />
      ) : !logsQ.data || logsQ.data.length === 0 ? (
        <EmptyState title="No reminders sent yet" description="Sent reminders will be logged here." />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-kfs-sage-100 text-left text-xs uppercase tracking-wide text-kfs-sage-600">
                <th className="px-4 py-2">Type</th>
                <th className="px-4 py-2">Recipient</th>
                <th className="px-4 py-2">Sent</th>
              </tr>
            </thead>
            <tbody>
              {logsQ.data.map((l) => (
                <tr key={l.id} className="border-b border-kfs-sage-50 last:border-0">
                  <td className="px-4 py-2">{l.type}</td>
                  <td className="px-4 py-2 text-kfs-sage-700">{l.studentEmail ?? '—'}</td>
                  <td className="px-4 py-2 text-kfs-sage-600">{formatRiyadhDate(l.sentAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}
