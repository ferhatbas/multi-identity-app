import { Button } from 'primereact/button';
import { useT } from '@/i18n';

export function SaveBar({ dirty, saving, canSave = true, onSave, onDiscard }: { dirty: boolean; saving: boolean; canSave?: boolean; onSave: () => void; onDiscard: () => void }) {
  const t = useT();
  return (
    <div className="save-bar" role="region" aria-label={t('Save')}>
      <span className="muted-text">{dirty ? t('You have unsaved changes.') : t('Nothing to save.')}</span>
      <Button text severity="secondary" label={t('Discard')} disabled={!dirty || saving} onClick={onDiscard} />
      <Button label={saving ? t('Saving…') : t('Save')} disabled={!dirty || !canSave} loading={saving} onClick={onSave} />
    </div>
  );
}
