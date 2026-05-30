import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, Input, LoadingPanel, EmptyState, PassTicketCard } from '@kfs/ui';
import { AdminPassType, PassOutputFormat } from '@kfs/types';
import type { ApiError, AdminPassType as PassTypeT, PassBatchSummaryDto, PassQuotaDto } from '@kfs/types';
import { formatRiyadhDate } from '@kfs/utils';
import { api } from '../api';
import { RosterPanel } from '../components/RosterPanel';

// Pass types that have a named holder + email address (driven by a roster upload).
// VVIP / Guest fall outside this — VVIP is a pool of anonymous QRs and Guest is
// student-self-booked. Photographer/PA/Visitor/Emergency aren't always emailed in
// practice but admins can if they have a roster handy.
const ROSTER_TYPE_OPTIONS: { value: PassTypeT; label: string }[] = [
  { value: AdminPassType.Staff,             label: 'Staff' },
  { value: AdminPassType.Media,             label: 'Media' },
  { value: AdminPassType.Photographer,      label: 'Photographer' },
  { value: AdminPassType.PersonalAssistant, label: 'Personal Assistant' },
  { value: AdminPassType.Visitor,           label: 'Visitor' },
  { value: AdminPassType.Emergency,         label: 'Emergency' }
];

function RosterPanelWithTypePicker() {
  const [rosterType, setRosterType] = useState<PassTypeT>(AdminPassType.Staff);
  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <label className="text-sm font-semibold text-kfs-forest">Roster type:</label>
        <select className="input w-56" value={rosterType}
                onChange={(e) => setRosterType(Number(e.target.value) as PassTypeT)}>
          {ROSTER_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      </div>
      <RosterPanel key={rosterType} type={rosterType} />
    </div>
  );
}

const TYPE_OPTIONS: { value: PassTypeT; label: string }[] = [
  { value: AdminPassType.VVIP,              label: 'VVIP' },
  { value: AdminPassType.Guest,             label: 'Guest' },
  { value: AdminPassType.Staff,             label: 'Staff' },
  { value: AdminPassType.Media,             label: 'Media' },
  { value: AdminPassType.Photographer,      label: 'Photographer' },
  { value: AdminPassType.PersonalAssistant, label: 'Personal Assistant' },
  { value: AdminPassType.Visitor,           label: 'Visitor' },
  { value: AdminPassType.Emergency,         label: 'Emergency' }
];
// Index matches AdminPassType enum value (0..7).
const TYPE_NAME = [
  'VVIP', 'Guest', 'Staff', 'Media',
  'Photographer', 'Personal Assistant', 'Visitor', 'Emergency'
];

export default function PassesPage() {
  const qc = useQueryClient();
  const [type, setType] = useState<PassTypeT>(AdminPassType.Guest);
  const [count, setCount] = useState(10);
  const [format, setFormat] = useState<0 | 1>(PassOutputFormat.Pdf);
  const [previewBatch, setPreviewBatch] = useState<string | null>(null);
  const [filterType, setFilterType] = useState<PassTypeT | 'all'>('all');

  const quotaQ = useQuery({
    queryKey: ['admin', 'passQuota'],
    queryFn: () => api.admin.passes.quota()
  });

  const batchesQ = useQuery({
    queryKey: ['admin', 'passBatches'],
    queryFn: () => api.admin.passes.batches()
  });

  const previewQ = useQuery({
    queryKey: ['admin', 'passes', previewBatch],
    queryFn: () => api.admin.passes.list(previewBatch!),
    enabled: !!previewBatch
  });

  // Selecting a pass type to generate also filters the batches table to that type.
  // The "Show type" dropdown can still override this to "All types" afterwards.
  useEffect(() => {
    setFilterType(type);
    setPreviewBatch(null);
  }, [type]);

  const invalidateAll = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'passBatches'] });
    qc.invalidateQueries({ queryKey: ['admin', 'passQuota'] });
  };

  const generateM = useMutation({
    mutationFn: () => api.admin.passes.generate({ type, count, format }),
    onSuccess: (res) => {
      toast.success(`Generated ${res.count} ${TYPE_NAME[type]} passes.`);
      invalidateAll();
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Generation failed.')
  });

  // Generate the full configured limit in one PDF (for VVIP/Staff/Media print runs).
  const generateFullM = useMutation({
    mutationFn: (limit: number) => api.admin.passes.generate({ type, count: limit, format: PassOutputFormat.Pdf }),
    onSuccess: (res) => {
      toast.success(`Generated the full allocation — ${res.count} ${TYPE_NAME[type]} passes in one PDF.`);
      invalidateAll();
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Generation failed.')
  });

  const downloadM = useMutation({
    mutationFn: ({ b, fmt }: { b: PassBatchSummaryDto; fmt: 0 | 1 }) =>
      api.admin.passes.download(b.batchId, fmt, b.type),
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Download failed.')
  });

  const deleteM = useMutation({
    mutationFn: (b: PassBatchSummaryDto) => api.admin.passes.deleteBatch(b.batchId),
    onSuccess: (res) => {
      toast.success(`Deleted ${res.deleted} passes — quota freed for regeneration.`);
      if (previewBatch && previewBatch === res.batchId) setPreviewBatch(null);
      invalidateAll();
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Delete failed.')
  });

  const deleteAllM = useMutation({
    mutationFn: (t: PassTypeT | undefined) => api.admin.passes.deleteAll(t),
    onSuccess: (res) => {
      toast.success(`Deleted ${res.deleted} passes${res.type !== null ? '' : ' (all types)'} — quota freed.`);
      setPreviewBatch(null);
      invalidateAll();
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Delete-all failed.')
  });

  const confirmDeleteAll = () => {
    const list = batchesQ.data?.filter((b) => filterType === 'all' || b.type === filterType) ?? [];
    if (list.length === 0) { toast.info('Nothing to delete.'); return; }
    const scanned = list.reduce((s, b) => s + b.scannedPasses, 0);
    const label = filterType === 'all' ? 'ALL pass batches' : `all ${TYPE_NAME[filterType]} batches`;
    const total = list.reduce((s, b) => s + b.count, 0);
    const warn = scanned > 0 ? `\n\n⚠ ${scanned} of these have been scanned — their scan history will also be erased.` : '';
    if (window.confirm(`Delete ${label} — ${total} pass${total === 1 ? '' : 'es'} across ${list.length} batch${list.length === 1 ? '' : 'es'}?${warn}\n\nThis cannot be undone.`)) {
      deleteAllM.mutate(filterType === 'all' ? undefined : filterType);
    }
  };

  const confirmDelete = (b: PassBatchSummaryDto) => {
    const warn = b.scannedPasses > 0
      ? `\n\n⚠ This batch has ${b.scannedPasses} scanned pass${b.scannedPasses === 1 ? '' : 'es'} — their scan history will also be deleted.`
      : '';
    if (window.confirm(`Delete this ${TYPE_NAME[b.type]} batch (${b.count} pass${b.count === 1 ? '' : 'es'})?${warn}\n\nThis cannot be undone.`)) {
      deleteM.mutate(b);
    }
  };

  // Seats consumed per pass for the selected type (Guest passes seat a group of 3).
  const seatsPerPass = type === AdminPassType.Guest ? 3 : 1;
  const selectedQuota = quotaQ.data?.find((q) => q.type === type);
  const remaining = selectedQuota?.remaining ?? 0;
  const maxPasses = Math.floor(remaining / seatsPerPass);
  const wouldUse = count * seatsPerPass;
  const overLimit = !!selectedQuota && wouldUse > remaining;

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Passes</h1>
        <p className="text-sm text-kfs-sage-700">Generate printable QR passes for VVIP, Guest, Staff, Media, Photographer, Personal Assistant, Visitor and Emergency zones.</p>
      </div>

      {/* ---- Per-type limits & usage ---- */}
      <QuotaTable quota={quotaQ.data} loading={quotaQ.isLoading} />

      {/* ---- Roster upload (3-step: Upload → Generate → Email) ----
           Used for the email-able pass types where every holder has a name + address. */}
      <RosterPanelWithTypePicker />

      {/* ---- Generate ---- */}
      <Card className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-kfs-forest">Generate a batch</h2>
        <div className="flex flex-wrap items-end gap-4">
          <div>
            <label className="label" htmlFor="type">Pass type</label>
            <select id="type" className="input" value={type}
                    onChange={(e) => setType(Number(e.target.value) as PassTypeT)}>
              {TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
            </select>
          </div>
          <div className="w-28">
            <Input id="count" label="How many" type="number" min={1} max={1000} value={count}
                   onChange={(e) => setCount(Math.max(1, Math.min(1000, Number(e.target.value) || 1)))} />
          </div>
          <div>
            <label className="label" htmlFor="format">Output</label>
            <select id="format" className="input" value={format}
                    onChange={(e) => setFormat(Number(e.target.value) as 0 | 1)}>
              <option value={PassOutputFormat.Pdf}>PDF (one document)</option>
              <option value={PassOutputFormat.Zip}>ZIP (one image per pass)</option>
            </select>
          </div>
          <Button variant="accent" loading={generateM.isPending} disabled={overLimit || remaining <= 0}
                  onClick={() => generateM.mutate()}>
            Generate passes
          </Button>
          {selectedQuota && (
            <Button variant="secondary" loading={generateFullM.isPending}
                    onClick={() => generateFullM.mutate(selectedQuota.capacity)}
                    title="Generate the full configured limit as a single printable PDF">
              Generate all {selectedQuota.capacity} (PDF)
            </Button>
          )}
        </div>
        {selectedQuota && (
          <p className={`text-sm ${overLimit ? 'text-red-600' : 'text-kfs-sage-700'}`}>
            {TYPE_NAME[type]}: {remaining} of {selectedQuota.capacity} seats remaining
            {seatsPerPass > 1 ? ` (≈ ${maxPasses} more passes)` : ` (${maxPasses} more passes)`}.
            {' '}This batch uses {wouldUse} seat{wouldUse === 1 ? '' : 's'}.
            {overLimit && ' Reduce the count — it exceeds the limit.'}
          </p>
        )}
      </Card>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-base font-semibold text-kfs-forest">Generated batches</h2>
        <div className="flex items-center gap-2">
          <label className="label !mb-0" htmlFor="filter">Show type</label>
          <select id="filter" className="input py-1" value={filterType}
                  onChange={(e) => {
                    const v = e.target.value;
                    setFilterType(v === 'all' ? 'all' : (Number(v) as PassTypeT));
                    setPreviewBatch(null);
                  }}>
            <option value="all">All types</option>
            {TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
          </select>
          <Button variant="danger" loading={deleteAllM.isPending}
                  onClick={confirmDeleteAll}>
            Delete all{filterType === 'all' ? '' : ` ${TYPE_NAME[filterType]}`}
          </Button>
        </div>
      </div>
      {(() => {
        const batches = batchesQ.data?.filter((b) => filterType === 'all' || b.type === filterType);
        return batchesQ.isLoading ? (
        <LoadingPanel label="Loading batches…" />
      ) : !batches || batches.length === 0 ? (
        <EmptyState
          title={filterType === 'all' ? 'No batches yet' : `No ${TYPE_NAME[filterType]} batches`}
          description={filterType === 'all'
            ? 'Generate a batch above and it will appear here for download.'
            : 'No batches of this type yet — switch to "All types" or generate one above.'} />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-kfs-sage-100 text-left text-xs uppercase tracking-wide text-kfs-sage-600">
                <th className="px-4 py-2">Type</th>
                <th className="px-4 py-2">Passes</th>
                <th className="px-4 py-2">Seats</th>
                <th className="px-4 py-2">Scanned</th>
                <th className="px-4 py-2">Created</th>
                <th className="px-4 py-2 text-right">Download</th>
              </tr>
            </thead>
            <tbody>
              {batches.map((b) => (
                <tr key={b.batchId} className="border-b border-kfs-sage-50 last:border-0">
                  <td className="px-4 py-2 font-medium text-kfs-forest-700">{TYPE_NAME[b.type]}</td>
                  <td className="px-4 py-2">{b.count}</td>
                  <td className="px-4 py-2">{b.seatsTotal}</td>
                  <td className="px-4 py-2">
                    <span className={b.scannedPasses > 0 ? 'text-green-700' : 'text-kfs-sage-500'}>
                      {b.scannedPasses} / {b.count}
                    </span>
                  </td>
                  <td className="px-4 py-2 text-kfs-sage-600">{formatRiyadhDate(b.createdAt)}</td>
                  <td className="px-4 py-2">
                    <div className="flex justify-end gap-2">
                      <Button variant={previewBatch === b.batchId ? 'primary' : 'ghost'}
                              onClick={() => setPreviewBatch(previewBatch === b.batchId ? null : b.batchId)}>
                        {previewBatch === b.batchId ? 'Hide' : 'Preview'}
                      </Button>
                      <Button variant="secondary" loading={downloadM.isPending}
                              onClick={() => downloadM.mutate({ b, fmt: PassOutputFormat.Pdf })}>PDF</Button>
                      <Button variant="ghost" loading={downloadM.isPending}
                              onClick={() => downloadM.mutate({ b, fmt: PassOutputFormat.Zip })}>ZIP</Button>
                      <Button variant="danger" loading={deleteM.isPending && deleteM.variables?.batchId === b.batchId}
                              onClick={() => confirmDelete(b)}>Delete</Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      );
      })()}

      {previewBatch && (
        <div className="flex flex-col gap-3">
          <h2 className="text-base font-semibold text-kfs-forest">Pass preview</h2>
          {previewQ.isLoading ? (
            <LoadingPanel label="Loading passes…" />
          ) : !previewQ.data || previewQ.data.length === 0 ? (
            <EmptyState title="No passes in this batch" />
          ) : (
            <div className="grid gap-5 xl:grid-cols-2">
              {previewQ.data.map((p) => <PassTicketCard key={p.id} pass={p} />)}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** Per-type limit table: shows limit / generated / remaining, with an inline editable limit. */
function QuotaTable({ quota, loading }: { quota?: PassQuotaDto[]; loading: boolean }) {
  const qc = useQueryClient();
  const setQuotaM = useMutation({
    mutationFn: ({ type, capacity }: { type: PassTypeT; capacity: number }) =>
      api.admin.passes.setQuota(type, capacity),
    onSuccess: (q) => {
      toast.success(`${TYPE_NAME[q.type]} limit set to ${q.capacity}.`);
      qc.invalidateQueries({ queryKey: ['admin', 'passQuota'] });
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Could not update limit.')
  });

  if (loading) return <LoadingPanel label="Loading limits…" />;
  if (!quota) return null;

  const totals = quota.reduce(
    (a, q) => ({ cap: a.cap + q.capacity, issued: a.issued + q.issued, remaining: a.remaining + q.remaining }),
    { cap: 0, issued: 0, remaining: 0 }
  );

  return (
    <Card className="overflow-x-auto p-0">
      <div className="flex items-center justify-between px-4 pt-3">
        <h2 className="text-base font-semibold text-kfs-forest">Limits &amp; usage</h2>
        <span className="text-xs text-kfs-sage-600">
          Total: {totals.issued} generated · {totals.remaining} left of {totals.cap}
        </span>
      </div>
      <table className="mt-2 w-full text-sm">
        <thead>
          <tr className="border-b border-kfs-sage-100 text-left text-xs uppercase tracking-wide text-kfs-sage-600">
            <th className="px-4 py-2">Type</th>
            <th className="px-4 py-2">Limit (seats)</th>
            <th className="px-4 py-2">Generated</th>
            <th className="px-4 py-2">Remaining</th>
            <th className="px-4 py-2 w-1/3">Usage</th>
          </tr>
        </thead>
        <tbody>
          {quota.map((q) => <QuotaRow key={q.type} q={q} saving={setQuotaM.isPending} onSave={setQuotaM.mutate} />)}
        </tbody>
      </table>
    </Card>
  );
}

function QuotaRow({ q, saving, onSave }: {
  q: PassQuotaDto;
  saving: boolean;
  onSave: (v: { type: PassTypeT; capacity: number }) => void;
}) {
  const [limit, setLimit] = useState(q.capacity);
  // Keep the field in sync when the server value changes (e.g. after another edit).
  useEffect(() => setLimit(q.capacity), [q.capacity]);

  const pct = q.capacity > 0 ? Math.min(100, Math.round((q.issued / q.capacity) * 100)) : 0;
  const dirty = limit !== q.capacity;

  return (
    <tr className="border-b border-kfs-sage-50 last:border-0">
      <td className="px-4 py-2 font-medium text-kfs-forest-700">{TYPE_NAME[q.type]}</td>
      <td className="px-4 py-2">
        <div className="flex items-center gap-2">
          <input type="number" min={q.issued} className="input w-24 py-1" value={limit}
                 onChange={(e) => setLimit(Math.max(0, Number(e.target.value) || 0))} />
          <Button variant="secondary" disabled={!dirty} loading={saving}
                  onClick={() => onSave({ type: q.type, capacity: limit })}>Save</Button>
        </div>
      </td>
      <td className="px-4 py-2">{q.issued}</td>
      <td className="px-4 py-2 font-medium text-kfs-forest">{q.remaining}</td>
      <td className="px-4 py-2">
        <div className="h-2.5 w-full overflow-hidden rounded-full bg-kfs-sage-50">
          <div className="h-full rounded-full bg-kfs-forest transition-all" style={{ width: `${pct}%` }} />
        </div>
        <span className="text-xs text-kfs-sage-600">{pct}% issued</span>
      </td>
    </tr>
  );
}
