import { Link } from 'react-router-dom';
import { useT } from '@/i18n';

export function NotFoundPage() {
  const t = useT();
  return (
    <div className="empty card card-pad">
      <strong>{t('Page not found')}</strong>
      <p><Link to="/">{t('Back to the start')}</Link></p>
    </div>
  );
}
