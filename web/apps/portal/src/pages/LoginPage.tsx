import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useQuery } from '@tanstack/react-query';
import { toast } from 'sonner';
import { useAuthStore } from '@kfs/api-client';
import { RIYADH_TZ } from '@kfs/utils';
import type { ApiError } from '@kfs/types';
import { KfsLogo } from '@kfs/ui';
import { api } from '../api';
import './signin.css';

const schema = z.object({
  email: z.string().email(),
  password: z.string().min(6)
});
type FormValues = z.infer<typeof schema>;

// Format helpers in Asia/Riyadh wall-clock, Western digits.
const fmt = (iso: string, opts: Intl.DateTimeFormatOptions) =>
  new Intl.DateTimeFormat('en-GB', { timeZone: RIYADH_TZ, ...opts }).format(new Date(iso));

export default function LoginPage() {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const isAuthed = useAuthStore((s) => s.isAuthenticated());
  const [showPwd, setShowPwd] = useState(false);

  useEffect(() => {
    if (isAuthed) navigate('/', { replace: true });
  }, [isAuthed, navigate]);

  // Live banner data (pre-auth, safe aggregates). Falls back to the design's copy if unavailable.
  const eventQ = useQuery({
    queryKey: ['public', 'event'],
    queryFn: () => api.events.publicSummary(),
    staleTime: 60_000,
    retry: 0
  });
  const ev = eventQ.data ?? null;

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema)
  });

  const onSubmit = async (values: FormValues) => {
    try {
      const resp = await api.auth.login(values.email, values.password);
      setAuth(resp);
      navigate(resp.mustChangePassword ? '/change-password' : '/', { replace: true });
    } catch (e) {
      toast.error((e as ApiError)?.message ?? 'Sign-in failed. Check your credentials.');
    }
  };

  // Banner values: live where we have them, design copy as fallback.
  const dateLine = ev ? `${fmt(ev.eventDate, { weekday: 'short' })} · ${fmt(ev.eventDate, { day: 'numeric', month: 'long', year: 'numeric' })}` : 'Fri · 13 March 2026';
  const doorsLine = ev
    ? `Doors ${fmt(new Date(new Date(ev.eventDate).getTime() - 30 * 60000).toISOString(), { hour: 'numeric', minute: '2-digit' })} · Curtain ${fmt(ev.eventDate, { hour: 'numeric', minute: '2-digit' })}`
    : 'Doors 6:30 PM · Curtain 7:00 PM';
  const venueName = ev?.venue ?? 'The Auditorium';
  const venueAddr = ev?.venueAddress ?? 'South Campus, Block C';
  const seatsLine = ev ? `${ev.seatsRemaining} / ${ev.seatsTotal}` : '312 / 840';

  return (
    <div className="kfs-signin">
      <main className="page">
        {/* ─────────── LEFT · BANNER ─────────── */}
        <section className="banner" aria-label="Annual Function 2026 — King Faisal School">
          <div className="corners" aria-hidden="true"><i /><i /><i /><i /></div>

          <div className="banner-top">
            <span className="logo-block"><KfsLogo variant="emblem" height={56} href="/" /></span>
            <div className="arabic-line">
              <strong>الحفل السنوي ٢٠٢٦</strong>
              <span>إيمانٌ · علمٌ · عملٌ</span>
            </div>
          </div>

          <div className="banner-body">
            <div className="eyebrow"><span className="pip" /><span>The Annual Function · MMXXVI</span></div>

            <h1>An evening<br />of <em>music,</em><br />dance <span className="amp">&amp;</span> light.</h1>

            <p className="banner-ar">أمسيةٌ من <em>الموسيقى</em> والرقصِ والنور</p>

            <p className="banner-tag">
              One night. Six houses. Eighty-four performers on a single stage — a celebration of{' '}
              <strong style={{ color: 'var(--cream)', fontWeight: 500 }}>faith, knowledge and work</strong> at King Faisal School.
            </p>

            <div className="meta">
              <div>
                <div className="lbl">Date</div>
                <div className="val">{dateLine}<small>{doorsLine}</small></div>
              </div>
              <div>
                <div className="lbl">Venue</div>
                <div className="val">{venueName}<small>{venueAddr}</small></div>
              </div>
              <div>
                <div className="lbl">Runtime</div>
                <div className="val">2h 40m<small>One interval · 15 min</small></div>
              </div>
            </div>

            <div className="program" aria-label="Programme highlights">
              <span><em>01</em>Overture</span>
              <span><em>02</em>Qur'an Recitation</span>
              <span><em>03</em>Classical Ensemble</span>
              <span><em>04</em>Drama: The Long Light</span>
              <span><em>05</em>Contemporary Dance</span>
              <span><em>06</em>House Choir Finale</span>
            </div>
          </div>

          <div className="banner-foot">
            <div className="seats">
              <span className="dot" /><span>Seats remaining</span><strong>{seatsLine}</strong>
            </div>
            <div>Sign in → pick tier → choose seats</div>
          </div>
        </section>

        {/* ─────────── RIGHT · CONSOLE ─────────── */}
        <section className="console" aria-label="Sign in">
          <div className="console-body">
            <h1>Welcome <em>back.</em></h1>
            <p className="console-sub">Sign in with your KFS account to reserve seats. Parents, alumni, students and staff are all welcome.</p>

            <form onSubmit={handleSubmit(onSubmit)} noValidate>
              <div className="field">
                <label htmlFor="email">Email or admission no.</label>
                <div className="input">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="3" y="5" width="18" height="14" rx="1" /><path d="m3 7 9 6 9-6" /></svg>
                  <input id="email" type="text" placeholder="you@kfs.sch.sa" autoComplete="username" {...register('email')} />
                </div>
                {errors.email && <span style={{ color: '#b91c1c', fontSize: 12 }}>Enter a valid email.</span>}
              </div>

              <div className="field">
                <label htmlFor="pwd">Password</label>
                <div className="input">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6"><rect x="4" y="10" width="16" height="10" rx="1" /><path d="M8 10V7a4 4 0 0 1 8 0v3" /></svg>
                  <input id="pwd" type={showPwd ? 'text' : 'password'} placeholder="••••••••" autoComplete="current-password" {...register('password')} />
                  <button type="button" className="toggle" onClick={() => setShowPwd((v) => !v)}>{showPwd ? 'Hide' : 'Show'}</button>
                </div>
                {errors.password && <span style={{ color: '#b91c1c', fontSize: 12 }}>Password is required.</span>}
              </div>

              <div className="row">
                <label className="check"><input type="checkbox" defaultChecked /> Keep me signed in</label>
                <a className="fp" href="#" onClick={(e) => { e.preventDefault(); toast.info('Contact the school office to reset your password.'); }}>Forgot password</a>
              </div>

              <button className="signin" type="submit" disabled={isSubmitting}>
                <span>{isSubmitting ? 'Signing in…' : 'Sign in & book tickets'}</span>
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M5 12h14M13 6l6 6-6 6" /></svg>
              </button>
            </form>
          </div>

          <div className="console-bottom">
            <div>© 2026 · King Faisal School · Cultural Society</div>
            <div className="legal">
              <a href="#" onClick={(e) => e.preventDefault()}>Terms</a>
              <a href="#" onClick={(e) => e.preventDefault()}>Privacy</a>
            </div>
          </div>
        </section>
      </main>
    </div>
  );
}
