import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { LoadingPanel } from '@kfs/ui';
import { RequireAuth } from './components/RequireAuth';
import { Layout } from './components/Layout';

const LoginPage          = lazy(() => import('./pages/LoginPage'));
const ChangePasswordPage = lazy(() => import('./pages/ChangePasswordPage'));
const DashboardPage      = lazy(() => import('./pages/DashboardPage'));
const GroupSelectPage    = lazy(() => import('./pages/GroupSelectPage'));
const SeatMapPage        = lazy(() => import('./pages/SeatMapPage'));
const CartPage           = lazy(() => import('./pages/CartPage'));
const MyBookingsPage     = lazy(() => import('./pages/MyBookingsPage'));

export default function App() {
  return (
    <Suspense fallback={<LoadingPanel />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/change-password" element={
          <RequireAuth><ChangePasswordPage /></RequireAuth>
        } />

        <Route element={<RequireAuth><Layout /></RequireAuth>}>
          <Route path="/" element={<DashboardPage />} />
          <Route path="/book/group" element={<GroupSelectPage />} />
          <Route path="/book/seats" element={<SeatMapPage />} />
          <Route path="/cart" element={<CartPage />} />
          <Route path="/bookings" element={<MyBookingsPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Suspense>
  );
}
