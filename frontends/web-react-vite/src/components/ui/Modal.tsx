import type { ReactNode } from 'react';
import { Button } from 'primereact/button';
import { Dialog } from 'primereact/dialog';
import { useT } from '@/i18n';

interface ModalProps {
  title: string;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  width?: number;
  compact?: boolean;
}

export const MODAL_HEIGHT = 'min(620px, 88vh)';

export function Modal({ title, onClose, children, footer, width = 520, compact }: ModalProps) {
  return (
    <Dialog visible onHide={onClose} header={title} footer={footer} modal draggable={false} resizable={false} dismissableMask
            style={{ width, maxWidth: '95vw', ...(compact ? {} : { height: MODAL_HEIGHT }) }} contentStyle={{ paddingTop: 4 }}>
      {children}
    </Dialog>
  );
}

interface ConfirmProps {
  title: string;
  message: ReactNode;
  confirmLabel: string;
  danger?: boolean;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function ConfirmDialog({ title, message, confirmLabel, danger, busy, onConfirm, onCancel }: ConfirmProps) {
  const t = useT();
  return (
    <Modal
      title={title}
      onClose={onCancel}
      width={440}
      compact
      footer={<>
        <Button label={t('Cancel')} outlined severity="secondary" onClick={onCancel} disabled={busy} />
        <Button label={confirmLabel} severity={danger ? 'danger' : undefined} onClick={onConfirm} disabled={busy} loading={busy} autoFocus />
      </>}
    >
      <p className="muted-text">{message}</p>
    </Modal>
  );
}
