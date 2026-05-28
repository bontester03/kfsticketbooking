import { useCallback, useEffect, useRef, useState } from 'react';
import type { ScanResponse } from '@kfs/types';
import { KfsLogo } from '@kfs/ui';
import { api } from './api';

const TOKEN_KEY = 'kfs.scanner.token';

// Accept either a bare token or a full link (…?token=XXXX) pasted into the box.
function extractToken(raw: string): string {
  const s = raw.trim();
  const m = s.match(/[?&]token=([^&\s]+)/i);
  return m && m[1] ? decodeURIComponent(m[1]) : s;
}

type RecentScan = { id: number; valid: boolean; result: number; message: string; at: Date };

export default function App() {
  // Scanner token: from ?token= in the link, else previously saved, else prompt.
  const [token, setToken] = useState<string | null>(() => {
    const url = new URLSearchParams(window.location.search).get('token');
    if (url) { localStorage.setItem(TOKEN_KEY, url); return url; }
    return localStorage.getItem(TOKEN_KEY);
  });
  const [tokenInput, setTokenInput] = useState('');

  const [result, setResult] = useState<ScanResponse | null>(null);
  const [cameraError, setCameraError] = useState<string | null>(null);
  const [facing, setFacing] = useState<'environment' | 'user'>('environment');
  const [manual, setManual] = useState('');
  const [recent, setRecent] = useState<RecentScan[]>([]);

  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const rafRef = useRef<number>(0);
  const lastScan = useRef<{ payload: string; at: number }>({ payload: '', at: 0 });
  const busy = useRef(false);
  const tokenRef = useRef(token);
  tokenRef.current = token;

  const verify = useCallback(async (payload: string) => {
    if (busy.current || !tokenRef.current) return;
    busy.current = true;
    try {
      const res = await api.scan.verify(payload, tokenRef.current, navigator.userAgent);
      // A bad scanner token means every scan will fail — drop back to the token screen.
      if (!res.valid && (res.message ?? '').toLowerCase().includes('token')) {
        stopCamera();
        localStorage.removeItem(TOKEN_KEY);
        setToken(null);
        return;
      }
      setResult(res);
      setRecent((r) => [{ id: Date.now(), valid: res.valid, result: res.result, message: res.message ?? '', at: new Date() }, ...r].slice(0, 8));
      if (res.valid) { beep(880, 0.12); navigator.vibrate?.(120); }
      else { beep(220, 0.25); navigator.vibrate?.([80, 60, 80]); }
    } catch {
      setResult({ valid: false, result: 2, admittedCount: 0, alreadyScanned: false, message: 'Network error — try again.' } as ScanResponse);
    } finally {
      // Brief cooldown so the same code isn't re-submitted while still in frame.
      setTimeout(() => { busy.current = false; }, 1200);
    }
  }, []);

  const tick = useCallback(() => {
    const video = videoRef.current, canvas = canvasRef.current;
    if (video && canvas && video.readyState === video.HAVE_ENOUGH_DATA && typeof jsQR !== 'undefined') {
      const w = video.videoWidth, h = video.videoHeight;
      if (w && h) {
        canvas.width = w; canvas.height = h;
        const ctx = canvas.getContext('2d', { willReadFrequently: true })!;
        ctx.drawImage(video, 0, 0, w, h);
        const img = ctx.getImageData(0, 0, w, h);
        const code = jsQR(img.data, w, h, { inversionAttempts: 'dontInvert' });
        if (code?.data) {
          const now = Date.now();
          if (code.data !== lastScan.current.payload || now - lastScan.current.at > 3000) {
            lastScan.current = { payload: code.data, at: now };
            void verify(code.data);
          }
        }
      }
    }
    rafRef.current = requestAnimationFrame(tick);
  }, [verify]);

  const stopCamera = useCallback(() => {
    cancelAnimationFrame(rafRef.current);
    streamRef.current?.getTracks().forEach((t) => t.stop());
    streamRef.current = null;
  }, []);

  const startCamera = useCallback(async () => {
    setCameraError(null);
    if (!navigator.mediaDevices?.getUserMedia) {
      setCameraError('Camera needs HTTPS. Open this page over https:// (or use manual entry below).');
      return;
    }
    try {
      stopCamera();
      const stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: facing }, audio: false });
      streamRef.current = stream;
      const v = videoRef.current!;
      v.srcObject = stream;
      v.setAttribute('playsinline', 'true');
      await v.play();
      rafRef.current = requestAnimationFrame(tick);
    } catch {
      setCameraError('Could not open the camera. Grant camera permission, or use manual entry below.');
    }
  }, [facing, stopCamera, tick]);

  useEffect(() => {
    if (token) void startCamera();
    return stopCamera;
  }, [token, facing, startCamera, stopCamera]);

  // ----- token entry screen -----
  if (!token) {
    return (
      <div className="grid min-h-screen place-items-center bg-kfs-forest px-4 text-white">
        <div className="w-full max-w-sm rounded-xl bg-white p-6 text-kfs-forest shadow-lg">
          <div className="mb-4 flex flex-col items-center gap-2 text-center">
            <KfsLogo />
            <h1 className="text-lg font-semibold">Gate Scanner</h1>
            <p className="text-sm text-kfs-sage-700">Paste the event scanner token to begin. (Admins can share a link with the token built in.)</p>
          </div>
          <input className="input mb-3 w-full" placeholder="Scanner token" value={tokenInput}
                 onChange={(e) => setTokenInput(e.target.value)} />
          <button className="btn-primary w-full" disabled={!tokenInput.trim()}
                  onClick={() => { const t = extractToken(tokenInput); localStorage.setItem(TOKEN_KEY, t); setToken(t); }}>
            Start scanning
          </button>
        </div>
      </div>
    );
  }

  const banner = bannerStyle(result);

  // ----- scanner screen -----
  return (
    <div className="flex min-h-screen flex-col bg-kfs-forest text-white">
      <header className="flex items-center justify-between px-4 py-3">
        <div className="flex items-center gap-2"><KfsLogo variant="emblem" height={28} /><span className="text-sm font-semibold">Gate Scanner</span></div>
        <div className="flex items-center gap-2">
          <button className="rounded-md border border-white/30 px-3 py-1 text-xs"
                  onClick={() => setFacing((f) => (f === 'environment' ? 'user' : 'environment'))}>
            {facing === 'environment' ? 'Rear cam' : 'Front cam'}
          </button>
          <button className="rounded-md border border-white/30 px-3 py-1 text-xs"
                  onClick={() => { stopCamera(); localStorage.removeItem(TOKEN_KEY); setToken(null); }}>
            Exit
          </button>
        </div>
      </header>

      {/* Camera viewport */}
      <div className="relative mx-auto aspect-square w-full max-w-md overflow-hidden rounded-xl bg-black">
        <video ref={videoRef} className="h-full w-full object-cover" muted playsInline />
        <canvas ref={canvasRef} className="hidden" />
        <div className="pointer-events-none absolute inset-[18%] rounded-lg border-2 border-white/70" />
        {cameraError && (
          <div className="absolute inset-0 grid place-items-center bg-black/70 p-6 text-center text-sm">{cameraError}</div>
        )}
      </div>

      {/* Result banner */}
      <div className={`mx-auto mt-4 w-full max-w-md rounded-xl px-5 py-4 text-center ${banner.bg}`}>
        {result ? (
          <>
            <div className="text-2xl font-bold">{banner.title}</div>
            <div className="mt-1 text-sm opacity-95">{result.message}</div>
            {(result.zone || result.seatLabel || result.holderName) && (
              <div className="mt-2 text-sm opacity-90">
                {result.zone}{result.seatLabel ? ` · Seat ${result.seatLabel}` : ''}{result.holderName ? ` · ${result.holderName}` : ''}
              </div>
            )}
            {result.seatsCount && result.seatsCount > 1 && (
              <div className="mt-1 text-xs font-semibold">Admitted {result.admittedCount} of {result.seatsCount}</div>
            )}
          </>
        ) : (
          <div className="text-sm opacity-90">Point the camera at a ticket QR code…</div>
        )}
      </div>

      {/* Manual fallback */}
      <details className="mx-auto mt-3 w-full max-w-md px-4 text-sm">
        <summary className="cursor-pointer opacity-80">Manual entry (no camera)</summary>
        <textarea className="input mt-2 w-full text-kfs-forest" rows={3} placeholder="Paste QR payload text"
                  value={manual} onChange={(e) => setManual(e.target.value)} />
        <button className="btn-accent mt-2 w-full" disabled={!manual.trim()} onClick={() => verify(manual.trim())}>Verify</button>
      </details>

      {/* Recent */}
      {recent.length > 0 && (
        <div className="mx-auto mt-4 w-full max-w-md px-4 pb-6">
          <div className="mb-1 text-xs uppercase tracking-wide opacity-70">Recent</div>
          <ul className="space-y-1 text-sm">
            {recent.map((r) => (
              <li key={r.id} className="flex items-center justify-between rounded-md bg-white/10 px-3 py-1.5">
                <span className={r.valid ? 'text-green-300' : 'text-red-300'}>{r.valid ? '✓' : '✕'} {r.message}</span>
                <span className="opacity-60">{r.at.toLocaleTimeString()}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function bannerStyle(result: ScanResponse | null) {
  if (!result) return { bg: 'bg-white/10', title: '' };
  if (result.valid) return { bg: 'bg-green-600', title: 'ADMIT ✓' };
  if (result.result === 1) return { bg: 'bg-amber-600', title: 'ALREADY USED' };
  if (result.result === 3) return { bg: 'bg-amber-600', title: 'EXPIRED' };
  return { bg: 'bg-red-600', title: 'INVALID ✕' };
}

// Short WebAudio beep for scan feedback.
let audioCtx: AudioContext | null = null;
function beep(freq: number, seconds: number) {
  try {
    audioCtx ??= new (window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext)();
    const osc = audioCtx.createOscillator();
    const gain = audioCtx.createGain();
    osc.frequency.value = freq;
    osc.connect(gain); gain.connect(audioCtx.destination);
    gain.gain.setValueAtTime(0.15, audioCtx.currentTime);
    osc.start();
    osc.stop(audioCtx.currentTime + seconds);
  } catch { /* ignore */ }
}
