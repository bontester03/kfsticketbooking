import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { LoadingPanel } from '@kfs/ui';
import { RequireAuth } from './components/RequireAuth';
import { Layout } from './components/Layout';

const LoginPage          = lazy(() => import('./pages/LoginPage'));
const ChangePasswordPage = lazy(() => import('./pages/ChangePasswordPage'));
const EventPickerPage    = lazy(() => import('./pages/EventPickerPage'));
const DashboardPage      = lazy(() => import('./pages/DashboardPage'));
const StudentsPage       = lazy(() => import('./pages/StudentsPage'));
const PassesPage         = lazy(() => import('./pages/PassesPage'));
const GuestPage          = lazy(() => import('./pages/GuestPage'));
const ScansPage          = lazy(() => import('./pages/ScansPage'));
const SeatMapPage        = lazy(() => import('./pages/SeatMapPage'));
const ReportsPage        = lazy(() => import('./pages/ReportsPage'));
const RemindersPage      = lazy(() => import('./pages/RemindersPage'));
const EventSettingsPage  = lazy(() => import('./pages/EventSettingsPage'));

export default function App() {
  return (
    <Suspense fallback={<LoadingPanel />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/change-password" element={
          <RequireAuth><ChangePasswordPage /></RequireAuth>
        } />

        {/* Post-login landing — the two-event picker (no Layout chrome). */}
        <Route path="/" element={<RequireAuth><EventPickerPage /></RequireAuth>} />

        {/* All admin pages nest under /:eventSlug/... so the URL itself records
            which event the admin is acting on (bookmarkable, shareable). */}
        <Route path="/:eventSlug" element={<RequireAuth><Layout /></RequireAuth>}>
          <Route index element={<Navigate to="dashboard" replace />} />
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="students"  element={<StudentsPage />} />
          <Route path="passes"    element={<PassesPage />} />
          <Route path="guest"     element={<GuestPage />} />
          <Route path="scans"     element={<ScansPage />} />
          <Route path="seatmap"   element={<SeatMapPage />} />
          <Route path="reports"   element={<ReportsPage />} />
          <Route path="reminders" element={<RemindersPage />} />
          <Route path="event"     element={<EventSettingsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  );
}
