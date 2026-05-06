import { useNavigate } from 'react-router-dom';
import { Button, Card } from '@kfs/ui';
import { useTranslation } from '@kfs/i18n';

export default function GroupSelectPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pick = (group: 'A' | 'B') => navigate(`/book/seats?group=${group}&side=Female`);

  return (
    <div className="grid gap-6">
      <header>
        <h1 className="text-xl font-semibold text-kfs-forest">{t('groupSelect.title')}</h1>
        <p className="mt-1 text-sm text-kfs-sage-700">{t('groupSelect.subtitle')}</p>
      </header>
      <div className="grid gap-4 sm:grid-cols-2">
        {(['A', 'B'] as const).map((g) => (
          <Card key={g} className="flex flex-col items-center gap-3 text-center">
            <div className="flex h-24 w-24 items-center justify-center rounded-full bg-kfs-forest text-3xl font-bold text-white">
              {g}
            </div>
            <h2 className="font-semibold text-kfs-forest">{t(`groupSelect.group${g}` as 'groupSelect.groupA' | 'groupSelect.groupB')}</h2>
            <Button onClick={() => pick(g)}>{t('groupSelect.title')} {g}</Button>
          </Card>
        ))}
      </div>
    </div>
  );
}
