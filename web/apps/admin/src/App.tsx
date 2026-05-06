import { Card, EmptyState, KfsLogo } from '@kfs/ui';

export default function App() {
  return (
    <div className="min-h-screen bg-kfs-forest-50/40">
      <header className="border-b border-kfs-sage-100 bg-white">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3">
          <KfsLogo />
          <span className="text-sm font-semibold uppercase tracking-wider text-kfs-sage-700">Admin Console</span>
        </div>
      </header>

      <main className="mx-auto max-w-6xl px-4 py-10">
        <Card className="mb-6">
          <h1 className="text-xl font-semibold text-kfs-forest">KFS Admin Console</h1>
          <p className="mt-2 text-sm text-kfs-sage-700">
            The full admin experience (students, Excel upload, live seat map, generate passes,
            reports, reminders, event settings) is part of the next pass — see DECISIONS.md.
            For now use Swagger at <code className="rounded bg-kfs-sage-50 px-1.5 py-0.5 font-mono text-xs">http://localhost:5080/swagger</code>
            with the seeded super-admin (<code>admin@kfs.sch.sa</code>).
          </p>
        </Card>
        <EmptyState
          title="Admin pages coming soon"
          description="The shell exists; pages will land in the next frontend pass."
        />
      </main>
    </div>
  );
}
