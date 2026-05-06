import { NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuthStore } from '@kfs/api-client';
import { Button, KfsLogo } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import clsx from 'clsx';

export function Layout() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const displayName = useAuthStore((s) => s.displayName);
  const clear = useAuthStore((s) => s.clear);

  const toggleLang = () => {
    const next = i18n.language === 'ar' ? 'en' : 'ar';
    void i18n.changeLanguage(next);
  };

  const onLogout = () => {
    clear();
    navigate('/login', { replace: true });
  };

  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b border-kfs-sage-100 bg-white">
        <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
          <div className="flex items-center gap-3">
            <KfsLogo />
          </div>
          <nav className="hidden gap-1 sm:flex">
            <NavLink to="/" end className={({ isActive }) => navCls(isActive)}>{t('nav.dashboard')}</NavLink>
            <NavLink to="/bookings" className={({ isActive }) => navCls(isActive)}>{t('nav.myBookings')}</NavLink>
          </nav>
          <div className="flex items-center gap-2">
            <span className="hidden text-sm text-kfs-sage-700 sm:inline">{displayName}</span>
            <Button variant="ghost" onClick={toggleLang}>{i18n.language === 'ar' ? 'EN' : 'ع'}</Button>
            <Button variant="secondary" onClick={onLogout}>{t('nav.logout')}</Button>
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6">
        <Outlet />
      </main>
    </div>
  );
}

function navCls(active: boolean) {
  return clsx(
    'inline-flex items-center rounded-md px-3 py-1.5 text-sm font-medium transition-colors',
    active ? 'bg-kfs-forest text-white' : 'text-kfs-forest-700 hover:bg-kfs-sage-50'
  );
}
