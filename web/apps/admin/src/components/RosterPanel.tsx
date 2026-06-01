import { useState, useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card } from '@kfs/ui';
import type {
  AdminPassDto, AdminPassType as PassTypeT, ApiError, RosterPreviewDto
} from '@kfs/types';
import { api } from '../api';
import { useEventContext } from '../lib/eventContext';

// Per-type label (matches PassesPage TYPE_NAME).
const LABEL: Record<number, string> = {
  0: 'VVIP', 1: 'Guest', 2: 'Staff', 3: 'Media',
  4: 'Photographer', 5: 'Personal Assistant', 6: 'Visitor', 7: 'Emergency'
};

interface Props {
  // Pass types eligible for the roster flow (email each holder their QR).
  type: PassTypeT;
}

/**
 * Three-step roster flow for staff-style passes:
 *   Step 1: pick file → POST /roster-preview → show summary (would-import / dupes / errors)
 *   Step 2: click Generate → POST /from-roster → creates AdminPass rows + QR images, no email
 *   Step 3: click Send All → POST /batches/{id}/send-emails → emails each holder their QR
 *           with per-row "Sent at HH:mm" status and individual Resend button.
 *
 * The same batchId is kept across steps so the admin can re-open this panel, see the list,
 * and resend individual emails any time.
 */
export function RosterPanel({ type }: Props) {
  const qc = useQueryClient();
  const eventId = useEventContext((s) => s.eventId);
  const fileRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<RosterPreviewDto | null>(null);
  const [batchId, setBatchId] = useState<string | null>(null);

  // List of passes for the current batch — drives the per-row Sent / Resend UI.
  const passesQ = useQuery({
    queryKey: ['admin', 'roster', 'batch', eventId, batchId],
    queryFn: () => api.admin.passes.list(batchId!),
    enabled: !!eventId && !!batchId,
    refetchInterval: batchId ? 5_000 : false
  });

  const previewM = useMutation({
    mutationFn: (f: File) => api.admin.passes.rosterPreview(type, f),
    onSuccess: (d) => setPreview(d),
    onError: (e: ApiError) => toast.error(e?.message ?? 'Preview failed')
  });

  const generateM = useMutation({
    mutationFn: (f: File) => api.admin.passes.generateFromRoster(type, f),
    onSuccess: (d) => {
      setBatchId(d.batchId);
      setPreview(null); setFile(null);
      if (fileRef.current) fileRef.current.value = '';
      toast.success(`Generated ${d.generated} ${LABEL[type]} pass${d.generated === 1 ? '' : 'es'}.${d.skipped ? ` ${d.skipped} duplicate(s) skipped.` : ''}`);
      void qc.invalidateQueries({ queryKey: ['admin', 'passes'] });
      void qc.invalidateQueries({ queryKey: ['admin', 'passQuota'] });
    },
    onError: (e: ApiError) => toast.error(e?.message ?? 'Generate failed')
  });

  const sendAllM = useMutation({
    mutationFn: ({ id, force }: { id: string; force: boolean }) =>
      api.admin.passes.sendBatchEmails(id, force),
    onSuccess: (d) => {
      toast.success(`Sent ${d.sent} email${d.sent === 1 ? '' : 's'}.${d.failed ? ` ${d.failed} failed.` : ''}${d.skipped ? ` ${d.skipped} already sent.` : ''}`);
      void qc.invalidateQueries({ queryKey: ['admin', 'roster', 'batch', batchId] });
    },
    onError: (e: ApiError) => toast.error(e?.message ?? 'Email batch failed')
  });

  const resendM = useMutation({
    mutationFn: (passId: string) => api.admin.passes.resendPassEmail(passId),
    onSuccess: () => {
      toast.success('Email resent.');
      void qc.invalidateQueries({ queryKey: ['admin', 'roster', 'batch', batchId] });
    },
    onError: (e: ApiError) => toast.error(e?.message ?? 'Resend failed')
  });

  const passes: AdminPassDto[] = passesQ.data ?? [];
  const total = passes.length;
  const sentCount = passes.filter(p => p.emailSent).length;
  const pendingCount = total - sentCount;
  const sentPct = total === 0 ? 0 : Math.round((sentCount / total) * 100);

  return (
    <Card>
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-base font-semibold text-kfs-forest">
          Roster upload — {LABEL[type]}
        </h2>
        {batchId && (
          <button type="button"
            onClick={() => { setBatchId(null); setPreview(null); setFile(null); }}
            className="text-xs text-kfs-sage-700 hover:underline">
            Start a new batch ↺
          </button>
        )}
      </div>
      <p className="mt-1 text-xs text-kfs-sage-600">
        Upload a <b>3-column</b> XLSX: <code>Full Name · Email · Type</code>. One QR per row, emailed
        to each holder. The <b>Type</b> column must equal <b>"{LABEL[type]}"</b> on every row —
        wrong-type rows are rejected so a misuploaded file can't silently issue the wrong passes.
        <button type="button"
          onClick={() => api.admin.passes.rosterSampleDownload(type, LABEL[type] ?? 'pass')
            .catch((e: ApiError) => toast.error(e?.message ?? 'Download failed.'))}
          className="ml-2 text-kfs-forest underline hover:no-underline">
          Download {LABEL[type]} template ↓
        </button>
      </p>

      {/* ---------- Step 1: Upload + Preview ---------- */}
      <div className="mt-4 flex flex-wrap items-center gap-3 border-t border-kfs-sage-100 pt-4">
        <span className="rounded-full bg-kfs-forest text-white text-xs font-bold h-6 w-6 grid place-items-center">1</span>
        <span className="font-medium text-sm text-kfs-forest">Upload roster</span>

        <input
          ref={fileRef}
          type="file"
          accept=".xlsx,.xls"
          onChange={(e) => { const f = e.target.files?.[0] ?? null; setFile(f); setPreview(null); }}
          className="text-xs text-kfs-sage-700"
        />
        <Button
          variant="secondary"
          disabled={!file || previewM.isPending}
          loading={previewM.isPending}
          onClick={() => file && previewM.mutate(file)}>
          Preview
        </Button>
      </div>

      {preview && (
        <div className="mt-3 ml-9 rounded-md border border-kfs-sage-100 bg-kfs-forest-50/30 p-3 text-xs">
          <div className="grid grid-cols-4 gap-3">
            <Stat label="Total rows" value={preview.totalRows} />
            <Stat label="Will import" value={preview.wouldImport} tone="good" />
            <Stat label="Duplicates" value={preview.wouldSkipDuplicates} tone="warn" />
            <Stat label="Errors" value={preview.errorRows} tone={preview.errorRows > 0 ? 'bad' : 'muted'} />
          </div>
          <p className="mt-2 text-[11px] text-kfs-sage-700">
            Quota for {LABEL[type]}: <b>{preview.quotaIssued}</b> of <b>{preview.quotaCapacity}</b> issued
            (remaining <b>{preview.quotaRemaining}</b>).
          </p>
          {preview.errors.length > 0 && (
            <ul className="mt-2 list-disc pl-4 text-red-700">
              {preview.errors.slice(0, 6).map(er => (
                <li key={er.rowNumber}>Row {er.rowNumber} — {er.field}: {er.message}</li>
              ))}
              {preview.errors.length > 6 && <li>… +{preview.errors.length - 6} more</li>}
            </ul>
          )}
        </div>
      )}

      {/* ---------- Step 2: Generate QRs ---------- */}
      <div className="mt-4 flex flex-wrap items-center gap-3 border-t border-kfs-sage-100 pt-4">
        <span className={`rounded-full ${preview ? 'bg-kfs-forest' : 'bg-kfs-sage-300'} text-white text-xs font-bold h-6 w-6 grid place-items-center`}>2</span>
        <span className="font-medium text-sm text-kfs-forest">Generate QR codes</span>
        <Button
          disabled={!file || !preview || preview.wouldImport === 0 || generateM.isPending}
          loading={generateM.isPending}
          onClick={() => file && generateM.mutate(file)}>
          Generate {preview?.wouldImport ?? 0} QR{preview?.wouldImport === 1 ? '' : 's'}
        </Button>
      </div>

      {/* ---------- Step 3: Email send ---------- */}
      <div className="mt-4 flex flex-wrap items-center gap-3 border-t border-kfs-sage-100 pt-4">
        <span className={`rounded-full ${batchId ? 'bg-kfs-forest' : 'bg-kfs-sage-300'} text-white text-xs font-bold h-6 w-6 grid place-items-center`}>3</span>
        <span className="font-medium text-sm text-kfs-forest">Email each holder their QR</span>
        <Button
          disabled={!batchId || pendingCount === 0 || sendAllM.isPending}
          loading={sendAllM.isPending}
          onClick={() => batchId && sendAllM.mutate({ id: batchId, force: false })}>
          Send to {pendingCount} pending
        </Button>
        {batchId && sentCount > 0 && (
          <Button
            variant="secondary"
            disabled={sendAllM.isPending}
            loading={sendAllM.isPending}
            onClick={() => batchId && sendAllM.mutate({ id: batchId, force: true })}>
            Re-send all {total} (force)
          </Button>
        )}
      </div>

      {/* ---------- Analytics + per-row list ---------- */}
      {batchId && (
        <div className="mt-5 border-t border-kfs-sage-100 pt-4">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <div className="text-sm text-kfs-forest">
              <b>{sentCount}</b> of <b>{total}</b> emails sent
              <span className="ml-2 text-kfs-sage-700 text-xs">({sentPct}%)</span>
            </div>
            <div className="h-2 w-48 overflow-hidden rounded-full bg-kfs-sage-100">
              <div className="h-full bg-emerald-500 transition-all" style={{ width: `${sentPct}%` }} />
            </div>
          </div>

          <div className="overflow-x-auto rounded-md border border-kfs-sage-100">
            <table className="w-full text-sm">
              <thead className="bg-kfs-forest-50/40 text-left text-xs uppercase tracking-wider text-kfs-sage-700">
                <tr>
                  <th className="px-3 py-2 w-10">#</th>
                  <th className="px-3 py-2">Name</th>
                  <th className="px-3 py-2">Email</th>
                  <th className="px-3 py-2">Ticket</th>
                  <th className="px-3 py-2">Email status</th>
                  <th className="px-3 py-2 w-32 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-kfs-sage-100">
                {passes.length === 0 && (
                  <tr><td colSpan={6} className="px-3 py-4 text-center text-xs text-kfs-sage-600">Loading…</td></tr>
                )}
                {passes.map(p => (
                  <tr key={p.id}>
                    <td className="px-3 py-2 text-xs text-kfs-sage-600">{p.sequenceNumber}</td>
                    <td className="px-3 py-2 font-medium">{p.issuedToName ?? '—'}</td>
                    <td className="px-3 py-2 text-xs">{p.issuedToEmail ?? '—'}</td>
                    <td className="px-3 py-2 text-xs font-mono text-kfs-sage-700">{p.ticketNumber.slice(-10)}</td>
                    <td className="px-3 py-2 text-xs">
                      {p.emailSent ? (
                        <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-emerald-800">
                          Sent {p.emailSentAt ? new Date(p.emailSentAt).toLocaleString() : ''}
                        </span>
                      ) : (
                        <span className="rounded-full bg-amber-50 px-2 py-0.5 text-amber-800">Pending</span>
                      )}
                    </td>
                    <td className="px-3 py-2 text-right">
                      <Button
                        variant="secondary"
                        disabled={!p.issuedToEmail || resendM.isPending}
                        onClick={() => resendM.mutate(p.id)}>
                        {p.emailSent ? 'Resend' : 'Send'}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </Card>
  );
}

function Stat({ label, value, tone = 'muted' }: { label: string; value: number; tone?: 'good' | 'warn' | 'bad' | 'muted' }) {
  const color =
    tone === 'good' ? 'text-emerald-700' :
    tone === 'warn' ? 'text-amber-700'   :
    tone === 'bad'  ? 'text-red-700'     :
                      'text-kfs-sage-800';
  return (
    <div>
      <p className="text-[10px] uppercase tracking-wider text-kfs-sage-600">{label}</p>
      <p className={`text-lg font-bold ${color}`}>{value}</p>
    </div>
  );
}
