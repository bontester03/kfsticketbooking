import { useNavigate } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Button, Card, CountdownPill, EmptyState, LoadingPanel } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import type { ApiError } from '@kfs/types';
import { ParentRole } from '@kfs/types';
import { api } from '../api';

export default function CartPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const qc = useQueryClient();

  const cartQ = useQuery({ queryKey: ['cart'], queryFn: api.cart.get, staleTime: 0 });

  const checkout = useMutation({
    mutationFn: api.cart.checkout,
    onSuccess: () => {
      toast.success(t('confirmation.title'));
      void qc.invalidateQueries({ queryKey: ['bookings'] });
      navigate('/bookings');
    },
    onError: (e: ApiError) => {
      toast.error(e?.code === 'cart_expired' ? t('errors.cartExpired') : (e?.message ?? t('errors.generic')));
      void qc.invalidateQueries({ queryKey: ['cart'] });
    }
  });

  const release = useMutation({
    mutationFn: api.cart.release,
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['cart'] });
      void qc.invalidateQueries({ queryKey: ['seatmap'] });
      navigate('/');
    }
  });

  if (cartQ.isLoading) return <LoadingPanel />;
  if (!cartQ.data || cartQ.data.items.length === 0) {
    return (
      <EmptyState
        title={t('dashboard.noBooking')}
        action={<Button onClick={() => navigate('/book/group')}>{t('dashboard.bookNow')}</Button>}
      />
    );
  }

  const cart = cartQ.data;
  const mother = cart.items.find(i => i.parentRole === ParentRole.Mother);
  const father = cart.items.find(i => i.parentRole === ParentRole.Father);

  return (
    <div className="grid gap-6">
      <header className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-kfs-forest">{t('cart.title')}</h1>
        {mother ? (
          <CountdownPill
            expiresAt={mother.holdExpiresAt}
            prefix={t('cart.expiresIn', { remaining: '' }).replace('{{remaining}}', '').trim()}
            onExpire={() => qc.invalidateQueries({ queryKey: ['cart'] })}
          />
        ) : null}
      </header>

      <div className="grid gap-4 sm:grid-cols-2">
        {[mother, father].filter(Boolean).map((item) => (
          <Card key={item!.id}>
            <div className="text-xs uppercase tracking-wider text-kfs-sage-700">
              {item!.parentRole === ParentRole.Mother ? t('cart.mother', { block: item!.block, seat: item!.fullLabel }) : t('cart.father', { block: item!.block, seat: item!.fullLabel })}
            </div>
            <div className="mt-2 text-3xl font-bold text-kfs-forest">{item!.fullLabel}</div>
            <div className="mt-1 text-sm text-kfs-sage-700">{item!.block}</div>
          </Card>
        ))}
      </div>

      <div className="flex flex-wrap gap-3">
        <Button onClick={() => checkout.mutate()} loading={checkout.isPending}>
          {t('cart.checkout')}
        </Button>
        <Button variant="secondary" onClick={() => release.mutate()} loading={release.isPending}>
          {t('cart.release')}
        </Button>
      </div>
    </div>
  );
}
