import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { useAuthStore } from '@kfs/api-client';
import type { ApiError } from '@kfs/types';
import { Button, Card, Input, KfsLogo } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';
import { api } from '../api';

const schema = z.object({
  currentPassword: z.string().min(6),
  newPassword: z.string().min(8).max(128),
  confirmPassword: z.string()
}).refine((v) => v.newPassword === v.confirmPassword, {
  message: 'Passwords do not match',
  path: ['confirmPassword']
});
type FormValues = z.infer<typeof schema>;

export default function ChangePasswordPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const auth = useAuthStore.getState();

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema)
  });

  const onSubmit = async (values: FormValues) => {
    try {
      await api.auth.changePassword(values.currentPassword, values.newPassword);
      // Re-issue the JWT so the mustChangePassword flag drops.
      const resp = await api.auth.login(auth.email!, values.newPassword);
      setAuth(resp);
      navigate('/', { replace: true });
    } catch (e) {
      const err = e as ApiError;
      toast.error(err?.message ?? t('errors.generic'));
    }
  };

  return (
    <div className="grid min-h-screen place-items-center px-4">
      <Card className="w-full max-w-sm">
        <div className="mb-4 flex flex-col items-center gap-2 text-center">
          <KfsLogo />
          <h1 className="text-base font-semibold text-kfs-forest">{t('auth.mustChangePassword')}</h1>
        </div>
        <form className="flex flex-col gap-3" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Input id="cur" label={t('auth.currentPassword')} type="password"
                 error={errors.currentPassword?.message} {...register('currentPassword')} />
          <Input id="new" label={t('auth.newPassword')} type="password"
                 error={errors.newPassword?.message} {...register('newPassword')} />
          <Input id="cnf" label={t('auth.confirmPassword')} type="password"
                 error={errors.confirmPassword?.message} {...register('confirmPassword')} />
          <Button type="submit" loading={isSubmitting}>{t('auth.save')}</Button>
        </form>
      </Card>
    </div>
  );
}
