import { useRef, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, Input, LoadingPanel, EmptyState } from '@kfs/ui';
import type { ApiError, StudentDto, StudentImportResultDto } from '@kfs/types';
import { formatRiyadhDate } from '@kfs/utils';
import { api } from '../api';

export default function StudentsPage() {
  const qc = useQueryClient();
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<'' | 'active' | 'inactive'>('');
  const fileRef = useRef<HTMLInputElement>(null);
  const [lastImport, setLastImport] = useState<StudentImportResultDto | null>(null);

  const studentsQ = useQuery({
    queryKey: ['admin', 'students', search, status],
    queryFn: () => api.admin.students.list(search || undefined, status || undefined)
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ['admin', 'students'] });

  const uploadM = useMutation({
    mutationFn: (file: File) => api.admin.students.upload(file),
    onSuccess: (res) => {
      setLastImport(res);
      toast.success(`Imported ${res.imported} of ${res.totalRows} rows (${res.skipped} skipped, ${res.failed} failed).`);
      invalidate();
    },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Upload failed.')
  });

  const activeM = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => api.admin.students.setActive(id, isActive),
    onSuccess: invalidate,
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Update failed.')
  });

  const resetM = useMutation({
    mutationFn: (id: string) => api.admin.students.resetPassword(id),
    onSuccess: (res) => toast.success(`New password: ${res.generatedPassword}`, { duration: 12_000 }),
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Reset failed.')
  });

  const deleteAllM = useMutation({
    mutationFn: () => api.admin.students.deleteAll(),
    onSuccess: (res) => { toast.success(`Deleted ${res.deleted} students (and their bookings/passes).`); invalidate(); },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Delete-all failed.')
  });

  const welcomeM = useMutation({
    mutationFn: () => api.admin.students.sendWelcomeEmails(),
    onSuccess: (res) => { toast.success(`Welcome emails queued for ${res.queued} students.`); invalidate(); },
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Send failed.')
  });

  const confirmSendWelcome = () => {
    const count = studentsQ.data?.length ?? 0;
    if (count === 0) { toast.info('No students to email.'); return; }
    const msg = `Send welcome emails to ${count} student${count === 1 ? '' : 's'}?\n\n` +
      `Every active student will have their password reset to their temporary password and receive an email with sign-in instructions.\n\n` +
      `Anyone who has already changed their password will lose that change.`;
    if (window.confirm(msg)) welcomeM.mutate();
  };

  const confirmDeleteAll = () => {
    const count = studentsQ.data?.length ?? 0;
    if (count === 0) { toast.info('No students to delete.'); return; }
    const msg = `Delete EVERY student?\n\nThis will permanently remove all ${count}+ student accounts AND their bookings, guest passes, and scan history.\n\nThis cannot be undone.`;
    if (!window.confirm(msg)) return;
    // Second guard for a truly irreversible action.
    const typed = window.prompt('Type DELETE to confirm:');
    if (typed !== 'DELETE') { toast.info('Cancelled.'); return; }
    deleteAllM.mutate();
  };

  const onPickFile = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) uploadM.mutate(file);
    if (fileRef.current) fileRef.current.value = '';
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-kfs-forest">Students</h1>
          <p className="text-sm text-kfs-sage-700">Manage student accounts, import rosters, reset passwords.</p>
        </div>
        <div className="flex items-center gap-3">
          <input ref={fileRef} type="file" accept=".xlsx,.xls" className="hidden" onChange={onPickFile} />
          <Button variant="ghost" onClick={() => api.admin.students.downloadSample()}>
            Download sample (.xlsx)
          </Button>
          <Button variant="accent" loading={uploadM.isPending} onClick={() => fileRef.current?.click()}>
            Upload roster (Excel)
          </Button>
          <Button variant="primary" loading={welcomeM.isPending} onClick={confirmSendWelcome}>
            Send welcome emails
          </Button>
          <Button variant="danger" loading={deleteAllM.isPending} onClick={confirmDeleteAll}>
            Delete all
          </Button>
        </div>
      </div>

      <Card className="border-l-4 border-l-kfs-sage-200 text-sm text-kfs-sage-700">
        <strong className="text-kfs-forest">Roster format (Excel .xlsx):</strong> columns in order —
        {' '}<code>Student ID</code>, <code>First Name</code>, <code>Last Name</code>,
        {' '}<code>Preferred Name</code> (Arabic), <code>Email</code>, <code>Gender</code>,
        {' '}<code>Grade</code>, <code>Group</code> (<em>VIP A</em> or <em>VIP B</em>).
        First row is a header. Download the sample to get the exact layout. Each student's
        temporary password becomes <code>First3letters + StudentID</code>, e.g. <code>Ahm437079</code>.
      </Card>

      {lastImport && (
        <Card className="border-l-4 border-l-kfs-gold">
          <p className="text-sm text-kfs-forest-700">
            Last import: <strong>{lastImport.imported}</strong> imported, {lastImport.skipped} skipped, {lastImport.failed} failed.
          </p>
          {lastImport.rowResults.filter((r) => !r.imported).length > 0 && (
            <ul className="mt-2 max-h-40 overflow-auto text-xs text-kfs-sage-700">
              {lastImport.rowResults.filter((r) => !r.imported).map((r) => (
                <li key={r.rowNumber}>Row {r.rowNumber}: {r.message}</li>
              ))}
            </ul>
          )}
        </Card>
      )}

      <Card className="flex flex-wrap items-end gap-3">
        <div className="min-w-[220px] flex-1">
          <Input id="search" label="Search" placeholder="Name or email…" value={search}
                 onChange={(e) => setSearch(e.target.value)} />
        </div>
        <div>
          <label className="label" htmlFor="status">Status</label>
          <select id="status" className="input" value={status}
                  onChange={(e) => setStatus(e.target.value as '' | 'active' | 'inactive')}>
            <option value="">All</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </div>
      </Card>

      {studentsQ.isLoading ? (
        <LoadingPanel label="Loading students…" />
      ) : !studentsQ.data || studentsQ.data.length === 0 ? (
        <EmptyState title="No students found" description="Adjust your search, or upload a roster to get started." />
      ) : (
        <Card className="overflow-x-auto p-0">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-kfs-sage-100 text-left text-xs uppercase tracking-wide text-kfs-sage-600">
                <th className="px-4 py-2">Name</th>
                <th className="px-4 py-2">Email</th>
                <th className="px-4 py-2">Booking</th>
                <th className="px-4 py-2">Seats</th>
                <th className="px-4 py-2">Status</th>
                <th className="px-4 py-2">Added</th>
                <th className="px-4 py-2 text-right">Actions</th>
              </tr>
            </thead>
            <tbody>
              {studentsQ.data.map((s) => <StudentRow key={s.id} s={s} activeM={activeM} resetM={resetM} />)}
            </tbody>
          </table>
        </Card>
      )}
    </div>
  );
}

function StudentRow({ s, activeM, resetM }: {
  s: StudentDto;
  activeM: { mutate: (v: { id: string; isActive: boolean }) => void; isPending: boolean };
  resetM: { mutate: (id: string) => void; isPending: boolean };
}) {
  return (
    <tr className="border-b border-kfs-sage-50 last:border-0">
      <td className="px-4 py-2 font-medium text-kfs-forest-700">{s.firstName} {s.lastName}</td>
      <td className="px-4 py-2 text-kfs-sage-700">{s.email}</td>
      <td className="px-4 py-2">{s.bookingStatus ?? '—'}</td>
      <td className="px-4 py-2 font-mono text-xs text-kfs-forest-700">{s.bookedSeats ?? '—'}</td>
      <td className="px-4 py-2">
        <span className={s.isActive ? 'text-kfs-forest' : 'text-red-600'}>
          {s.isActive ? 'Active' : 'Inactive'}
        </span>
      </td>
      <td className="px-4 py-2 text-kfs-sage-600">{formatRiyadhDate(s.createdAt, 'en', { dateStyle: 'medium' })}</td>
      <td className="px-4 py-2">
        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={() => resetM.mutate(s.id)}>Reset password</Button>
          <Button variant={s.isActive ? 'danger' : 'secondary'}
                  onClick={() => activeM.mutate({ id: s.id, isActive: !s.isActive })}>
            {s.isActive ? 'Deactivate' : 'Activate'}
          </Button>
        </div>
      </td>
    </tr>
  );
}
