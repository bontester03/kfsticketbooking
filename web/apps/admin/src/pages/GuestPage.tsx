import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, Input, LoadingPanel, EmptyState } from '@kfs/ui';
import type { ApiError } from '@kfs/types';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

export default function GuestPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');

  const eventId = useEventContext((s) => s.eventId);
  const eventQ = useQuery({
    queryKey: ['admin', 'event', eventId],
    queryFn: () => api.admin.event.get(eventId!),
    enabled: !!eventId
  });
  const analyticsQ = useQuery({
    queryKey: ['admin', 'guestAnalytics', eventId],
    queryFn: () => api.admin.guest.analytics(),
    enabled: !!eventId,
    refetchInterval: 15_000
  });

  // Gate-scanner deep link (tokened, no login). Scanner app default port 5175 locally;
  // override with VITE_SCANNER_URL for the deployed host.
  const viteEnv = (import.meta as unknown as { env?: Record<string, string> }).env;
  const runtimeConfig = (globalThis as unknown as { __KFS_CONFIG__?: { scannerUrl?: string } }).__KFS_CONFIG__;
  const scannerBase = runtimeConfig?.scannerUrl || viteEnv?.VITE_SCANNER_URL || window.location.origin.replace(/:\d+$/, ':5175');
  const scannerLink = eventQ.data?.scannerToken
    ? `${scannerBase}/?token=${encodeURIComponent(eventQ.data.scannerToken)}`
    : null;
  const studentsQ = useQuery({
    queryKey: ['admin', 'guestStudents', eventId, search],
    queryFn: () => api.admin.guest.students(search || undefined),
    enabled: !!eventId
  });

  const issue = useMutation({
    mutationFn: (studentId: string) => api.admin.guest.issue(studentId),
    onSuccess: () => {
      toast.success('Guest ticket issued to the student.');
      qc.invalidateQueries({ queryKey: ['admin', 'guestStudents'] });
      qc.invalidateQueries({ queryKey: ['admin', 'guestAnalytics'] });
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Could not issue the guest ticket.')
  });

  const a = analyticsQ.data;
  const stats = a ? [
    { label: 'Limit (seats)', value: a.limit, hint: 'Guest zone capacity' },
    { label: 'Issued', value: a.issued, hint: 'seats allocated' },
    { label: 'Remaining', value: a.remaining, hint: 'seats left' },
    { label: 'Guest tickets', value: a.passesTotal, hint: 'QR codes (each admits 3)' },
    { label: 'Booked by students', value: a.bookedByStudents, hint: 'self-service' },
    { label: 'Issued by admin', value: a.issuedByAdminToChild, hint: 'to a child' },
    { label: 'Admitted', value: a.admittedPeople, hint: 'scanned at the gate' }
  ] : [];

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Guest tickets</h1>
        <p className="text-sm text-kfs-sage-700">Each guest ticket is one QR that admits 3 people. Analytics auto-refresh every 15s.</p>
      </div>

      {scannerLink && (
        <Card className="flex flex-wrap items-center justify-between gap-3 border-l-4 border-l-kfs-gold">
          <div className="min-w-0">
            <div className="text-sm font-semibold text-kfs-forest">Gate scanner link</div>
            <div className="truncate text-xs text-kfs-sage-700">{scannerLink}</div>
            <div className="text-[11px] text-kfs-sage-600">Open on the iPad (over HTTPS for the camera). No login — the token is built in.</div>
          </div>
          <Button variant="secondary" onClick={() => { navigator.clipboard?.writeText(scannerLink); toast.success('Scanner link copied.'); }}>
            Copy link
          </Button>
        </Card>
      )}

      {analyticsQ.isLoading ? (
        <LoadingPanel label="Loading analytics…" />
      ) : (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
          {stats.map((s) => (
            <Card key={s.label} className="flex flex-col gap-1">
              <span className="text-2xl font-bold text-kfs-forest">{s.value}</span>
              <span className="text-xs font-medium text-kfs-forest-700">{s.label}</span>
              <span className="text-[11px] text-kfs-sage-600">{s.hint}</span>
            </Card>
          ))}
        </div>
      )}

      <div>
        <h2 className="text-base font-semibold text-kfs-forest">Issue a guest ticket to a child</h2>
        <p className="text-sm text-kfs-sage-700">Only children without a guest ticket can be issued one.</p>
      </div>

      <Card className="max-w-md">
        <Input id="search" label="Search students" placeholder="Name or email…" value={search}
               onChange={(e) => setSearch(e.target.value)} />
      </Card>

      {studentsQ.isLoading ? (
        <LoadingPanel label="Loading students…" />
      ) : !studentsQ.data || studentsQ.data.length === 0 ? (
        <EmptyState title="No students found" description="Adjust your search." />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-kfs-sage-100 text-left text-xs uppercase tracking-wide text-kfs-sage-600">
                <th className="px-4 py-2">Name</th>
                <th className="px-4 py-2">Email</th>
                <th className="px-4 py-2">Guest ticket</th>
                <th className="px-4 py-2 text-right">Action</th>
              </tr>
            </thead>
            <tbody>
              {studentsQ.data.map((s) => (
                <tr key={s.id} className="border-b border-kfs-sage-50 last:border-0">
                  <td className="px-4 py-2 font-medium text-kfs-forest-700">{s.fullName}</td>
                  <td className="px-4 py-2 text-kfs-sage-700">{s.email}</td>
                  <td className="px-4 py-2">
                    {s.hasGuestPass
                      ? <span className="text-kfs-forest">✓ Has one</span>
                      : <span className="text-kfs-sage-600">—</span>}
                  </td>
                  <td className="px-4 py-2 text-right">
                    <Button variant="accent" disabled={s.hasGuestPass || issue.isPending}
                            onClick={() => issue.mutate(s.id)}>
                      Issue guest ticket
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}
