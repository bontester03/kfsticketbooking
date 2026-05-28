import { useMutation } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card } from '@kfs/ui';
import type { ApiError } from '@kfs/types';
import { api } from '../api';

type Group = 'A' | 'B';
type Format = 'csv' | 'xlsx' | 'pdf';

export default function ReportsPage() {
  const downloadM = useMutation({
    mutationFn: ({ group, format }: { group: Group; format: Format }) =>
      api.admin.reports.download(group, format),
    onError: (e) => toast.error((e as unknown as ApiError)?.message ?? 'Download failed.')
  });

  const formats: { f: Format; label: string }[] = [
    { f: 'xlsx', label: 'Excel (.xlsx)' },
    { f: 'csv',  label: 'CSV' },
    { f: 'pdf',  label: 'PDF' }
  ];

  return (
    <div className="flex flex-col gap-4">
      <div>
        <h1 className="text-xl font-semibold text-kfs-forest">Reports</h1>
        <p className="text-sm text-kfs-sage-700">Export the seating manifest per VIP group — parent name, linked student, seat and time.</p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {(['A', 'B'] as Group[]).map((g) => (
          <Card key={g} className="flex flex-col gap-3">
            <h2 className="text-base font-semibold text-kfs-forest">VIP Group {g}</h2>
            <p className="text-sm text-kfs-sage-700">Full seating list for Group {g} (Female + Male sides).</p>
            <div className="flex flex-wrap gap-2">
              {formats.map(({ f, label }) => (
                <Button key={f} variant={f === 'xlsx' ? 'accent' : 'secondary'}
                        loading={downloadM.isPending && downloadM.variables?.group === g && downloadM.variables?.format === f}
                        onClick={() => downloadM.mutate({ group: g, format: f })}>
                  {label}
                </Button>
              ))}
            </div>
          </Card>
        ))}
      </div>
    </div>
  );
}
