import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { toast } from 'sonner';
import { useAuthStore } from '@kfs/api-client';
import type { ApiError } from '@kfs/types';
import { Button, Card, Input, KfsLogo } from '@kfs/ui';
import { api } from '../api';

const schema = z.object({
  email: z.string().email(),
  password: z.string().min(6)
});
type FormValues = z.infer<typeof schema>;

export default function LoginPage() {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const isAuthed = useAuthStore((s) => s.isAuthenticated());

  useEffect(() => {
    if (isAuthed) navigate('/', { replace: true });
  }, [isAuthed, navigate]);

  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<FormValues>({
    resolver: zodResolver(schema)
  });

  const onSubmit = async (values: FormValues) => {
    try {
      const resp = await api.auth.adminLogin(values.email, values.password);
      setAuth(resp);
      navigate(resp.mustChangePassword ? '/change-password' : '/', { replace: true });
    } catch (e) {
      const err = e as ApiError;
      toast.error(err?.message ?? 'Sign-in failed. Check your credentials.');
    }
  };

  return (
    <div className="grid min-h-screen place-items-center px-4">
      <Card className="w-full max-w-sm">
        <div className="mb-6 flex flex-col items-center gap-2 text-center">
          <KfsLogo />
          <h1 className="text-lg font-semibold text-kfs-forest">Admin Console</h1>
          <p className="text-xs text-kfs-sage-700">King Faisal School — Event Management</p>
        </div>
        <form className="flex flex-col gap-4" onSubmit={handleSubmit(onSubmit)} noValidate>
          <Input
            id="email"
            label="Email"
            type="email"
            autoComplete="email"
            error={errors.email?.message}
            {...register('email')}
          />
          <Input
            id="password"
            label="Password"
            type="password"
            autoComplete="current-password"
            error={errors.password?.message}
            {...register('password')}
          />
          <Button type="submit" loading={isSubmitting}>Sign in</Button>
        </form>
      </Card>
    </div>
  );
}
