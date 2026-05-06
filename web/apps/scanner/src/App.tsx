import { Card, EmptyState, KfsLogo } from '@kfs/ui';

export default function App() {
  return (
    <div className="grid min-h-screen place-items-center bg-kfs-forest text-white">
      <Card className="w-full max-w-sm bg-white text-kfs-forest">
        <div className="flex flex-col items-center gap-3 text-center">
          <KfsLogo />
          <h1 className="text-lg font-semibold">Gate Scanner</h1>
          <p className="text-sm text-kfs-sage-700">
            Camera, IndexedDB offline queue, and the iPad mirror-correction toggle ship in the
            next pass — see DECISIONS.md. The scanner verify endpoint
            (<code className="font-mono">POST /api/v1/scan/verify</code>) is already live on
            the API.
          </p>
        </div>
        <div className="mt-6">
          <EmptyState
            title="Scanner UI coming soon"
            description="Backend QR validation is fully implemented and tested."
          />
        </div>
      </Card>
    </div>
  );
}
