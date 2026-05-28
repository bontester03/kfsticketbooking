import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Card, Input, LoadingPanel, EmptyState } from '@kfs/ui';
import type { ScanAuditRow } from '@kfs/types';
import { formatRiyadhDate } from '@kfs/utils';
import { api } from '../api';

type Status = '' | 'scanned' | 'unscanned';

export default function ScansPage() {
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<Status>('');
  const [kind, setKind] = useState('');

  const q = useQuery({
    queryKey: ['admin', 'scans', search, status, kind],
    queryFn: () => api.admin.scans(search || undefined, status || undefined, kind || undefined),
    refetchInterval: 15_000
  });

  const KINDS = ['VVIP', 'Guest', 'Staff', 'Media', 'Seat'];

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Scan audit</h1>
        <p className="text-sm text-kfs-sage-700">
          Every ticket and whether it was scanned at the gate, and when. Auto-refreshes every 15s.
        </p>
      </div>

      {q.data && (
        <div className="grid grid-cols-3 gap-3">
          <Stat label="Tickets" value={q.data.totalTickets} />
          <Stat label="Scanned" value={q.data.scannedTickets} hint={`${q.data.totalTickets - q.data.scannedTickets} not scanned`} />
          <Stat label="People admitted" value={q.data.admittedPeople} hint="total valid scans" />
        </div>
      )}

      <Card className="flex flex-wrap items-end gap-3">
        <div className="min-w-[240px] flex-1">
          <Input id="search" label="Search" placeholder="Ticket number or name…" value={search}
                 onChange={(e) => setSearch(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="kind">Ticket type</label>
          <select id="kind" className="input" value={kind} onChange={(e) => setKind(e.target.value)}>
            <option value="">All types</option>
            {KINDS.map((k) => <option key={k} value={k}>{k === 'Seat' ? 'Student seat' : k}</option>)}
          </select>
        </div>
        <div>
          <label className="label" htmlFor="status">Show</label>
          <select id="status" className="input" value={status} onChange={(e) => setStatus(e.target.value as Status)}>
            <option value="">All tickets</option>
            <option value="scanned">Scanned only</option>
            <option value="unscanned">Not scanned</option>
          </select>
        </div>
      </Card>

      {q.isLoading ? (
        <LoadingPanel label="Loading scan audit…" />
      ) : !q.data || q.data.rows.length === 0 ? (
        <EmptyState title="Nothing to show" description="No tickets match this filter." />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-kfs-sage-100 text-left text-xs uppercase tracking-wide text-kfs-sage-600">
                <th className="px-4 py-2">Type</th>
                <th className="px-4 py-2">Ticket</th>
                <th className="px-4 py-2">Holder</th>
                <th className="px-4 py-2">Detail</th>
                <th className="px-4 py-2">Scanned</th>
                <th className="px-4 py-2">When</th>
              </tr>
            </thead>
            <tbody>
              {q.data.rows.map((r) => <Row key={`${r.kind}-${r.ticketNumber}`} r={r} />)}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: number; hint?: string }) {
  return (
    <Card className="flex flex-col gap-1">
      <span className="text-2xl font-bold text-kfs-forest">{value}</span>
      <span className="text-sm font-medium text-kfs-forest-700">{label}</span>
      {hint && <span className="text-xs text-kfs-sage-600">{hint}</span>}
    </Card>
  );
}

function Row({ r }: { r: ScanAuditRow }) {
  const multi = r.seatsCount > 1;
  return (
    <tr className="border-b border-kfs-sage-50 last:border-0">
      <td className="px-4 py-2 font-medium text-kfs-forest-700">{r.kind}</td>
      <td className="px-4 py-2 font-mono text-xs text-kfs-sage-700">{r.ticketNumber}</td>
      <td className="px-4 py-2">{r.holder ?? '—'}</td>
      <td className="px-4 py-2 text-kfs-sage-700">{r.detail ?? '—'}</td>
      <td className="px-4 py-2">
        {r.scanned ? (
          <span className="font-medium text-green-700">
            ✓ Yes{multi ? ` (${r.admittedCount}/${r.seatsCount})` : ''}
          </span>
        ) : (
          <span className="text-kfs-sage-500">— No</span>
        )}
      </td>
      <td className="px-4 py-2 text-kfs-sage-600">
        {r.lastScannedAt
          ? (multi && r.firstScannedAt && r.firstScannedAt !== r.lastScannedAt
              ? `${formatRiyadhDate(r.firstScannedAt)} → ${formatRiyadhDate(r.lastScannedAt, 'en', { timeStyle: 'short' })}`
              : formatRiyadhDate(r.lastScannedAt))
          : '—'}
      </td>
    </tr>
  );
}
